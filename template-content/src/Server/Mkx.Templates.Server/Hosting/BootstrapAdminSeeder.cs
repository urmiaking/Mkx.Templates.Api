using Microsoft.AspNetCore.Identity;
using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Server.Hosting;
public static class BootstrapAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider provider, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        var username = configuration["BootstrapAdmin:Username"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin enabled but Username/Password not configured.");
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync("Administrator"))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>("Administrator"));
            if (!roleResult.Succeeded) throw new InvalidOperationException("Failed to create administrator role.");
        }
        // Never overwrite an existing user's credentials or silently promote it.
        if (await users.FindByNameAsync(username) is not null) return;
        var user = new AppUser { UserName = username, DisplayName = "Administrator", EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException("Bootstrap admin validation failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        result = await users.AddToRoleAsync(user, "Administrator");
        if (!result.Succeeded) throw new InvalidOperationException("Failed to grant administrator role.");
    }
}
