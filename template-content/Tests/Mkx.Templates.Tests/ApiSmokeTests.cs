using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Mkx.Templates.Tests;
public sealed class ApiSmokeTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('K', 64),
                    ["BootstrapAdmin:Enabled"] = "false",
                    ["Database:ApplyMigrationsOnStartup"] = "false"
                }));
        });

    [Fact]
    public async Task LiveEndpointIsAvailableWithoutDatabase()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerCannotAccessMyAccount()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerCannotListUsers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDescribesAuthEndpoints()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("/api/auth/refresh", json);
    }
}

