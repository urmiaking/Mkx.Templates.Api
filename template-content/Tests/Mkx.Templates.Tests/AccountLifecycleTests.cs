using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Mkx.Templates.Server.Hosting;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Application.Services;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class AccountLifecycleTests
{
    private sealed class FakeSender : IAccountMessageSender
    {
        public readonly List<(string Recipient, string Subject, string Message)> Sent = [];
        public Task SendAsync(string recipient, string subject, string message, CancellationToken ct)
        {
            Sent.Add((recipient, subject, message));
            return Task.CompletedTask;
        }
        public string LastToken => Sent[^1].Message.Split("\n\n")[1];
    }

    private static async Task<(ServiceProvider Provider, SqliteConnection Connection)> CreateAsync()
    {
        var db = new SqliteConnection("DataSource=:memory:");
        await db.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<FakeSender>();
        services.AddSingleton<IAccountMessageSender>(s => s.GetRequiredService<FakeSender>());
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(db));
        services.AddIdentityCore<AppUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12;
            o.Password.RequiredUniqueChars = 4;
            o.Password.RequireDigit = false;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.SignIn.RequireConfirmedAccount = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()
          .AddDefaultTokenProviders();
        services.AddScoped<IAccountLifecycleService, AccountLifecycleService>();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return (provider, db);
    }

    private static RegisterAccountRequest Registration(string name = "michael") =>
        new(name, name + "@example.test", "Strong local test password 87!", "Demo User");

    [Fact]
    public async Task RegisterRequiresEmailConfirmationAndCannotBeRepeated()
    {
        var (provider, connection) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IAccountLifecycleService>();
        var sender = provider.GetRequiredService<FakeSender>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await service.RegisterAsync(Registration(), ct));
        var registered = await users.FindByNameAsync("michael");
        Assert.NotNull(registered);
        Assert.False(registered.EmailConfirmed);
        Assert.Single(sender.Sent);
        Assert.Equal("michael@example.test", sender.Sent[0].Recipient);
        Assert.False(await service.ConfirmAsync(new("michael", "invalid"), ct));
        var code = sender.LastToken;
        Assert.True(await service.ConfirmAsync(new("michael", code), ct));
        Assert.False(await service.ConfirmAsync(new("michael", code), ct));
        Assert.True((await users.FindByNameAsync("michael"))!.EmailConfirmed);
        Assert.False(await service.RegisterAsync(Registration(), ct));
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task RecoveryChecksUsernameAndEmailAndInvalidatesUsedResetToken()
    {
        var (provider, connection) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IAccountLifecycleService>();
        var sender = provider.GetRequiredService<FakeSender>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await service.RegisterAsync(Registration(), ct));
        await service.ConfirmAsync(new("michael", sender.LastToken), ct);

        await service.RequestPasswordResetAsync(new("michael", "wrong@example.test"), ct);
        await service.RequestPasswordResetAsync(new("no-such-account", "none@example.test"), ct);
        Assert.Single(sender.Sent);

        await service.RequestPasswordResetAsync(new("michael", "michael@example.test"), ct);
        Assert.Equal(2, sender.Sent.Count);
        var token = sender.LastToken;
        Assert.False(await service.ResetPasswordAsync(new("michael", "wrong", "Different password 95!"), ct));
        Assert.True(await service.ResetPasswordAsync(new("michael", token, "Different password 95!"), ct));
        Assert.False(await service.ResetPasswordAsync(new("michael", token, "Another different password 96!"), ct));
        var user = await users.FindByNameAsync("michael");
        Assert.True(await users.CheckPasswordAsync(user!, "Different password 95!"));
        Assert.False(await users.CheckPasswordAsync(user!, Registration().Password));
    }

    [Fact]
    public async Task BootstrapAdminWithOnboardingEnabledNeedsValidEmailAndCanBeCreated()
    {
        var incomplete = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Enabled"] = "true",
                ["BootstrapAdmin:Username"] = "first-admin",
                ["BootstrapAdmin:Password"] = "Strong initial admin password 89!",
                ["AccountLifecycle:Enabled"] = "true"
            }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BootstrapAdminSeeder.SeedAsync(null!, incomplete));

        var (provider, connection) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Enabled"] = "true",
                ["BootstrapAdmin:Username"] = "first-admin",
                ["BootstrapAdmin:Password"] = "Strong initial admin password 89!",
                ["BootstrapAdmin:Email"] = "admin@example.test",
                ["AccountLifecycle:Enabled"] = "true"
            }).Build();
        await BootstrapAdminSeeder.SeedAsync(provider, enabled);
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByNameAsync("first-admin");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("admin@example.test", user.Email);
        Assert.True(await users.IsInRoleAsync(user, "Administrator"));
    }

    [Fact]
    public async Task AdministratorCannotRequestAnonymousPasswordRecovery()
    {
        var (provider, connection) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var sender = provider.GetRequiredService<FakeSender>();
        var service = provider.GetRequiredService<IAccountLifecycleService>();
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Administrator"))).Succeeded);

        var admin = new AppUser { UserName = "admin-one", Email = "admin@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "Secret password for admin 87!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "Administrator")).Succeeded);

        await service.RequestPasswordResetAsync(new("admin-one", "admin@example.test"), ct);
        Assert.Empty(sender.Sent);
        Assert.False(await service.ResetPasswordAsync(
            new("admin-one", "invalid", "New strong admin password 88!"), ct));
    }

    [Fact]
    public async Task UnconfirmedAndMfaUsersCannotRecoverPasswords()
    {
        var (provider, connection) = await CreateAsync();
        await using var _ = provider;
        await using var __ = connection;
        var service = provider.GetRequiredService<IAccountLifecycleService>();
        var sender = provider.GetRequiredService<FakeSender>();
        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await service.RegisterAsync(Registration(), ct));
        await service.RequestPasswordResetAsync(new("michael", "michael@example.test"), ct);
        Assert.Single(sender.Sent);

        var token = sender.LastToken;
        await service.ConfirmAsync(new("michael", token), ct);
        var user = await users.FindByNameAsync("michael");
        Assert.NotNull(user);
        user.TwoFactorEnabled = true;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        await service.RequestPasswordResetAsync(new("michael", "michael@example.test"), ct);
        Assert.Single(sender.Sent);
        Assert.False(await service.ResetPasswordAsync(new("michael", "invalid", "Different password 95!"), ct));
    }
}
