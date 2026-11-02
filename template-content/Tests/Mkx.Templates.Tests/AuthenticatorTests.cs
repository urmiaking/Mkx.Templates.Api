using System.Net;
using System.Security.Cryptography;
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

public sealed class AuthenticatorTests
{
    private const string Password = "Secure Identity Password 49!";

    private sealed class FakeTokens : IAccessTokenIssuer
    {
        public (string Token, DateTimeOffset ExpiresAt) Issue(
            AppUser user, IReadOnlyList<string> roles) =>
            ("access-token-only-after-verified-factors", DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private static async Task<(ServiceProvider, SqliteConnection, AppUser)> CreateAsync()
    {
        var db = new SqliteConnection("DataSource=:memory:");
        await db.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessTokenIssuer, FakeTokens>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(db));
        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()
          .AddDefaultTokenProviders();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthenticatorService, AuthenticatorService>();
        services.AddScoped<IAuthenticatorReplayGuard, AuthenticatorReplayGuard>();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = "test-mfa-user",
            Email = "totp@example.test",
            EmailConfirmed = true
        };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        return (provider, db, user);
    }

    [Fact]
    public async Task TotpEnrollmentRequiresPasswordAndVerifiedCodeAndInvalidatesOldRefresh()
    {
        var (provider, connection, user) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var auth = provider.GetRequiredService<IAuthService>();
        var mfa = provider.GetRequiredService<IAuthenticatorService>();
        var ct = TestContext.Current.CancellationToken;

        var initial = await auth.LoginAsync(new LoginRequest(user.UserName!, Password), ct);
        Assert.NotNull(initial);
        Assert.Null(await mfa.BeginSetupAsync(user.Id, "incorrect", ct));
        var setup = await mfa.BeginSetupAsync(user.Id, Password, ct);
        Assert.NotNull(setup);
        Assert.Contains("otpauth://totp/", setup.AuthenticatorUri);
        Assert.False((await mfa.StatusAsync(user.Id, ct))!.Enabled);
        Assert.Null(await mfa.EnableAsync(user.Id, "bogus", ct));
        var correct = CurrentCode(setup.SharedKey);
        var enabled = await mfa.EnableAsync(user.Id, correct, ct);
        Assert.NotNull(enabled);
        Assert.Equal(8, enabled.RecoveryCodes.Count);
        Assert.True((await mfa.StatusAsync(user.Id, ct))!.Enabled);
        Assert.Null(await auth.RefreshAsync(new RefreshRequest(initial.RefreshToken), ct));
        Assert.Null(await auth.LoginAsync(new LoginRequest(user.UserName!, Password), ct));
        Assert.Null(await auth.LoginAsync(new LoginRequest(user.UserName!, "wrong-password", correct), ct));

        var nextCode = CurrentCode(setup.SharedKey, 1);
        var successful = await auth.LoginAsync(new LoginRequest(user.UserName!, Password, nextCode), ct);
        Assert.NotNull(successful);
        Assert.NotEmpty(successful.RefreshToken); // SQL Server smoke covers refresh rotation; SQLite lacks DateTimeOffset range SQL translation.

        Assert.Null(await auth.LoginAsync(new LoginRequest(user.UserName!, Password, nextCode), ct));
        var firstRecovery = enabled.RecoveryCodes[0];
        var usingRecovery = await auth.LoginAsync(
            new LoginRequest(user.UserName!, Password, RecoveryCode: firstRecovery), ct);
        Assert.NotNull(usingRecovery);
        Assert.Null(await auth.LoginAsync(
            new LoginRequest(user.UserName!, Password, RecoveryCode: firstRecovery), ct));

        Assert.False(await mfa.DisableAsync(user.Id, new AuthenticatorDisableRequest("wrong", correct), ct));
        Assert.True(await mfa.DisableAsync(user.Id,
            new AuthenticatorDisableRequest(Password, RecoveryCode: enabled.RecoveryCodes[1]), ct));
        Assert.False((await mfa.StatusAsync(user.Id, ct))!.Enabled);
        Assert.Null(await auth.RefreshAsync(new RefreshRequest(usingRecovery.RefreshToken), ct));
        Assert.NotNull(await auth.LoginAsync(new LoginRequest(user.UserName!, Password), ct));
    }

    [Fact]
    public async Task UnconfirmedAndLockedAccountsCannotStartAuthenticatorEnrollment()
    {
        var (provider, connection, user) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var mfa = provider.GetRequiredService<IAuthenticatorService>();
        var ct = TestContext.Current.CancellationToken;

        user.EmailConfirmed = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.Null(await mfa.BeginSetupAsync(user.Id, Password, ct));
        user.EmailConfirmed = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(30);
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.Null(await mfa.BeginSetupAsync(user.Id, Password, ct));
    }

    private static string CurrentCode(string key, int stepOffset = 0)
    {
        // RFC 6238 TOTP, standard 30-second timestep, SHA-1.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var next = 0;
        var decoded = new List<byte>();
        foreach (var ch in key.TrimEnd('=').ToUpperInvariant())
        {
            next = (next << 5) | alphabet.IndexOf(ch);
            bits += 5;
            if (bits < 8) continue;
            bits -= 8;
            decoded.Add((byte)(next >> bits));
            next &= (1 << bits) - 1;
        }

        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + stepOffset;
        Span<byte> msg = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(msg, step);
        using var hmac = new HMACSHA1(decoded.ToArray());
        var hash = hmac.ComputeHash(msg.ToArray());
        var offset = hash[^1] & 0x0f;
        var number = ((hash[offset] & 0x7f) << 24) |
                     (hash[offset + 1] << 16) |
                     (hash[offset + 2] << 8) | hash[offset + 3];
        return (number % 1000000).ToString("D6");
    }
}
