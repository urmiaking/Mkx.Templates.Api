using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mkx.Templates.Server.Auth;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class OpaqueIntrospectionTests
{
    private static WebApplicationFactory<Program> Factory(
        string? result = null, bool enabled = true, string endpoint = "https://id.example.test/oauth/introspect") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('K', 64),
                    ["BootstrapAdmin:Enabled"] = "false",
                    ["OpaqueIntrospection:Enabled"] = enabled.ToString(),
                    ["OpaqueIntrospection:Endpoint"] = endpoint,
                    ["OpaqueIntrospection:Issuer"] = "https://id.example.test",
                    ["OpaqueIntrospection:Audience"] = "service-api",
                    ["OpaqueIntrospection:ClientId"] = "test-resource-client",
                    ["OpaqueIntrospection:ClientSecret"] = "test-only-secret"
                }));
            web.ConfigureTestServices(services =>
                services.AddHttpClient(OpaqueIntrospectionHandler.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new StubProvider(result ??
                        Response(true, "service-api", "https://id.example.test"))));
        });

    [Fact]
    public async Task IntrospectionIsDisabledByDefaultWithoutNetworkAccess()
    {
        using var factory = Factory(enabled: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "opaque-test-value");
        using var response = await client.GetAsync("/api/auth/opaque/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActiveScopedTokenWithCorrectIssuerAudienceAndExpiryIsAccepted()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "opaque-test-value");
        using var response = await client.GetAsync("/api/auth/opaque/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("upstream-person-123", json.RootElement.GetProperty("subject").GetString());
    }

    [Theory]
    [InlineData(false, "service-api", "https://id.example.test")]
    [InlineData(true, "wrong-audience", "https://id.example.test")]
    [InlineData(true, "service-api", "https://evil.example.test")]
    public async Task InactiveWrongAudienceOrWrongIssuerIsRejected(bool active, string audience, string issuer)
    {
        using var factory = Factory(Response(active, audience, issuer));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "opaque-test-value");
        using var response = await client.GetAsync("/api/auth/opaque/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        using var factory = Factory(Response(true, "service-api", "https://id.example.test", expiresIn: -60));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "opaque-test-value");
        using var response = await client.GetAsync("/api/auth/opaque/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void InsecureIntrospectionEndpointFailsClosedAtStartup()
    {
        using var factory = Factory(endpoint: "http://id.example.test/oauth/introspect");
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task MissingTokenCannotAccessOpaqueEndpoint()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/auth/opaque/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Response(bool active, string audience, string issuer, int expiresIn = 900) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["active"] = active,
            ["sub"] = "upstream-person-123",
            ["iss"] = issuer,
            ["aud"] = audience,
            ["scope"] = "api:read",
            ["exp"] = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToUnixTimeSeconds()
        });

    private sealed class StubProvider(string result) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://id.example.test/oauth/introspect", request.RequestUri?.ToString());
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("token=opaque-test-value", form);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(result, Encoding.UTF8, "application/json")
            };
        }
    }
}
