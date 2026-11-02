using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Application.Services;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class ExternalAccountLinkTests
{
    private static async Task<(ServiceProvider, SqliteConnection)> NewDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityCore<AppUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IExternalAccountLinkService, ExternalAccountLinkService>();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return (provider, connection);
    }

    [Fact]
    public async Task ChallengeCanLinkOnceAndCannotBeStolenByAnotherExternalSubject()
    {
        var (provider, connection) = await NewDb();
        await using var connectionCleanup = connection;
        await using var providerCleanup = provider;
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = "user1", EmailConfirmed = true };
        var other = new AppUser { UserName = "user2", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.CreateAsync(other)).Succeeded);
        var links = provider.GetRequiredService<IExternalAccountLinkService>();
        var ct = TestContext.Current.CancellationToken;
        var code = await links.StartAsync(user.Id, ct);
        Assert.NotNull(code);
        Assert.Equal(64, code.Challenge.Length);
        Assert.False(await links.CompleteAsync("https://issuer.test", "", code.Challenge, ct));
        Assert.True(await links.CompleteAsync("https://issuer.test", "person1", code.Challenge, ct));
        Assert.False(await links.CompleteAsync("https://issuer.test", "person2", code.Challenge, ct));
        Assert.Equal(user.Id, (await links.ResolveAsync("https://issuer.test", "person1", ct))!.Id);
        Assert.Null(await links.ResolveAsync("https://issuer.test", "person2", ct));
        var otherCode = await links.StartAsync(other.Id, ct);
        Assert.NotNull(otherCode);
        Assert.False(await links.CompleteAsync("https://issuer.test", "person1", otherCode.Challenge, ct));
        Assert.True(await links.UnlinkAsync(user.Id, "https://issuer.test", ct));
        Assert.Null(await links.ResolveAsync("https://issuer.test", "person1", ct));
    }

    [Fact]
    public async Task AdminAndMfaAccountsCannotStartLink()
    {
        var (provider, connection) = await NewDb();
        await using var connectionCleanup = connection;
        await using var providerCleanup = provider;
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Administrator"))).Succeeded);
        var admin = new AppUser { UserName = "admin1", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "Administrator")).Succeeded);
        var mfa = new AppUser { UserName = "mfa1", EmailConfirmed = true, TwoFactorEnabled = true };
        Assert.True((await users.CreateAsync(mfa)).Succeeded);
        var links = provider.GetRequiredService<IExternalAccountLinkService>();
        Assert.Null(await links.StartAsync(admin.Id, TestContext.Current.CancellationToken));
        Assert.Null(await links.StartAsync(mfa.Id, TestContext.Current.CancellationToken));
    }
}
