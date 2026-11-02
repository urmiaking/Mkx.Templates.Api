using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Application.Services;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class PasskeyTests
{
    private const string Password = "Secure Passkey Password 32!";
    private sealed class FakeTokens : IAccessTokenIssuer
    {
        public (string Token, DateTimeOffset ExpiresAt) Issue(
            AppUser user, IReadOnlyList<string> roles)
            => ("test-jwt-issued-after-assertion", DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private static async Task<(ServiceProvider Provider, SqliteConnection Connection, AppUser User)> SetupAsync()
    {
        var sql = new SqliteConnection("DataSource=:memory:");
        await sql.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddHttpContextAccessor();
        services.Configure<IdentityPasskeyOptions>(o =>
        {
            o.ServerDomain = "localhost";
            o.ChallengeSize = 48;
            o.UserVerificationRequirement = "required";
            o.ResidentKeyRequirement = "required";
            o.ValidateOrigin = ctx => ValueTask.FromResult(
                string.Equals(ctx.Origin, "https://localhost", StringComparison.Ordinal));
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessTokenIssuer, FakeTokens>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(sql));
        services.AddIdentityCore<AppUser>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Password.RequiredUniqueChars = 4;
            o.Password.RequireDigit = false;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.SignIn.RequireConfirmedAccount = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()
          .AddSignInManager().AddDefaultTokenProviders();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(p => p.GetRequiredService<AuthService>());
        services.AddScoped<IAuthenticatorReplayGuard, AuthenticatorReplayGuard>();
        services.AddScoped<IPasskeyService, PasskeyService>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = "passkey-user", DisplayName = "Demo User", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        return (provider, sql, user);
    }

    private static DefaultHttpContext HttpsContext() =>
        new() { Request = { Scheme = "https", Host = new HostString("localhost") } };

    [Fact]
    public async Task RegistrationOptionsAreBoundToLocalUserAndProofAndExpire()
    {
        var (provider, connection, user) = await SetupAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IPasskeyService>();
        var db = provider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var context = HttpsContext();

        Assert.Null(await service.BeginRegistrationAsync(user.Id, new("wrong"), context, ct));
        var challenge = await service.BeginRegistrationAsync(user.Id, new(Password), context, ct);
        Assert.NotNull(challenge);
        Assert.Contains("challenge", challenge.OptionsJson);
        Assert.NotEqual(Guid.Empty, challenge.CeremonyId);

        var pending = await db.PasskeyCeremonies.AsNoTracking()
            .SingleAsync(x => x.Id == challenge.CeremonyId, ct);
        Assert.Equal("register", pending.Purpose);
        Assert.Equal(user.Id, pending.UserId);
        Assert.False(string.IsNullOrWhiteSpace(pending.State));
        Assert.True(pending.ExpiresAtUtc > DateTime.UtcNow);

        // The wrong account cannot consume the owner's challenge.
        Assert.False(await service.FinishRegistrationAsync(Guid.NewGuid(),
            new(challenge.CeremonyId, "{}"), context, ct));
        // An invalid response consumes its challenge; no replay is possible.
        Assert.False(await service.FinishRegistrationAsync(user.Id,
            new(challenge.CeremonyId, "{}"), context, ct));
        Assert.False(await service.FinishRegistrationAsync(user.Id,
            new(challenge.CeremonyId, "{}"), context, ct));
        Assert.NotNull((await db.PasskeyCeremonies.AsNoTracking()
            .SingleAsync(x => x.Id == challenge.CeremonyId, ct)).ConsumedAtUtc);
    }

    [Fact]
    public async Task AnonymousDiscoverableOptionsDoNotLeakUserAndCannotBeReplayed()
    {
        var (provider, connection, user) = await SetupAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IPasskeyService>();
        var db = provider.GetRequiredService<AppDbContext>();
        var context = HttpsContext();
        var ct = TestContext.Current.CancellationToken;

        var challenge = await service.BeginLoginAsync(context, ct);
        Assert.Contains("challenge", challenge.OptionsJson);
        Assert.DoesNotContain(user.UserName!, challenge.OptionsJson);
        var saved = await db.PasskeyCeremonies.AsNoTracking()
            .SingleAsync(x => x.Id == challenge.CeremonyId, ct);
        Assert.Equal("login", saved.Purpose);
        Assert.Null(saved.UserId);

        Assert.Null(await service.FinishLoginAsync(new(challenge.CeremonyId, "{}"), context, ct));
        Assert.Null(await service.FinishLoginAsync(new(challenge.CeremonyId, "{}"), context, ct));
        Assert.NotNull((await db.PasskeyCeremonies.AsNoTracking()
            .SingleAsync(x => x.Id == challenge.CeremonyId, ct)).ConsumedAtUtc);
    }

    [Fact]

    public async Task ExpiredCeremoniesCannotBeUsedAndArePruned()
    {
        var (provider, connection, user) = await SetupAsync();
        await using var cleanup = provider;
        await using var dbCleanup = connection;
        var service = provider.GetRequiredService<IPasskeyService>();
        var db = provider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var expired = new PasskeyCeremony
        {
            Purpose = "login",
            State = "untrusted-expired-state",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.PasskeyCeremonies.Add(expired);
        await db.SaveChangesAsync(ct);

        Assert.Null(await service.FinishLoginAsync(new(expired.Id, "{}"), HttpsContext(), ct));
        Assert.Null((await db.PasskeyCeremonies.AsNoTracking()
            .SingleAsync(x => x.Id == expired.Id, ct)).ConsumedAtUtc);

        var next = await service.BeginLoginAsync(HttpsContext(), ct);
        Assert.NotEqual(expired.Id, next.CeremonyId);
        Assert.False(await db.PasskeyCeremonies.AsNoTracking()
            .AnyAsync(x => x.Id == expired.Id, ct));
    }

    [Fact]
    public async Task MfaEnabledAccountCannotEnrollWithoutSecondFactor()
    {
        var (provider, connection, user) = await SetupAsync();
        await using var cleanup = provider;
        await using var dbCleanup = connection;
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        user.TwoFactorEnabled = true;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        var service = provider.GetRequiredService<IPasskeyService>();
        Assert.Null(await service.BeginRegistrationAsync(user.Id, new(Password),
            HttpsContext(), TestContext.Current.CancellationToken));
        Assert.False(await service.RemoveAsync(user.Id,
            new("nonexistent", Password), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task PasskeyListStartsEmptyAndUnconfirmedUsersCannotEnroll()
    {
        var (provider, connection, user) = await SetupAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IPasskeyService>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var ct = TestContext.Current.CancellationToken;
        var found = await service.ListAsync(user.Id, ct);
        Assert.NotNull(found);
        Assert.Empty(found);
        user.EmailConfirmed = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.Null(await service.BeginRegistrationAsync(user.Id, new(Password), HttpsContext(), ct));
    }
}
