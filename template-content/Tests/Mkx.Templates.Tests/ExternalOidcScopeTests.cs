using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class ExternalOidcScopeTests
{
    private static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('J', 64),
                    ["BootstrapAdmin:Enabled"] = "false",
                    ["ExternalOidc:RequiredScope"] = "mkx.api.read"
                }));
        });

    [Theory]
    [InlineData("mkx.api.read", true)]
    [InlineData("openid profile mkx.api.read", true)]
    [InlineData("mkx.api.write", false)]
    [InlineData("openid profile", false)]
    [InlineData("", false)]
    public async Task RequiresExplicitApiResourceScope(string scope, bool allowed)
    {
        using var factory = Factory();
        _ = factory.CreateClient();
        using var services = factory.Services.CreateScope();
        var authorization = services.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var claims = new List<Claim> { new("client_id", "mkx.api.local.smoke") };
        if (!string.IsNullOrWhiteSpace(scope)) claims.Add(new Claim("scope", scope));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "ExternalOidc"));
        var result = await authorization.AuthorizeAsync(principal, "ExternalOidcOnly");
        Assert.Equal(allowed, result.Succeeded);
    }
}
