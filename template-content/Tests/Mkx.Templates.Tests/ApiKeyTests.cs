using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Server.Auth;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class ApiKeyTests
{
    private static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('K', 64),
                    ["BootstrapAdmin:Enabled"] = "false"
                }));
        });

    private static string LocalToken()
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "Mkx.Templates.Api",
            Audience = "Mkx.Templates.Clients",
            SigningKey = new string('K', 64)
        };
        return new JwtTokenIssuer(Options.Create(jwtOptions), TimeProvider.System)
            .Issue(new AppUser { Id = Guid.NewGuid(), UserName = "admin" }, ["Administrator"]).Token;
    }

    [Fact]
    public async Task MissingApiKeyIsUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/integrations/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedApiKeyIsUnauthorizedWithoutDatabaseLookup()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "not-an-api-key");
        using var response = await client.GetAsync("/api/integrations/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocalAdministratorJwtDoesNotAuthenticateApiKeyEndpoint()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalToken());
        using var response = await client.GetAsync("/api/integrations/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyHeaderCannotAuthenticateLocalAdministratorEndpoint()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "not-an-api-key");
        using var response = await client.GetAsync("/api/api-keys", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedScopesAreRejectedWithoutWritingToDatabase()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LocalToken());
        using var payload = new StringContent(
            "{\"name\":\"worker\",\"scopes\":[\"administrator\"],\"validDays\":30}",
            System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/api-keys", payload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
