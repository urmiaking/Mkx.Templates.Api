using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class OpenApiContractTests
{
    private static WebApplicationFactory<Program> Factory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment(environment);
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('K', 64),
                    ["BootstrapAdmin:Enabled"] = "false"
                }));
        });

    [Fact]
    public async Task AllControllerEndpointsHaveStableUniqueOperationIdsTagsAndSecurity()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken));

        var root = json.RootElement;
        Assert.Equal("Mkx • API", root.GetProperty("info").GetProperty("title").GetString());
        var definitions = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(definitions.TryGetProperty("LocalJwt", out _));
        Assert.True(definitions.TryGetProperty("ExternalOidc", out _));
        Assert.True(definitions.TryGetProperty("OpaqueToken", out _));
        Assert.True(definitions.TryGetProperty("ApiKey", out var apiKeyScheme));
        Assert.Equal("X-API-Key", apiKeyScheme.GetProperty("name").GetString());
        Assert.Equal("header", apiKeyScheme.GetProperty("in").GetString());

        var paths = root.GetProperty("paths");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var operations = 0;
        foreach (var path in paths.EnumerateObject())
        foreach (var method in path.Value.EnumerateObject())
        {
            if (method.Name is not ("get" or "post" or "put" or "patch" or "delete"))
                continue;

            var operation = method.Value;
            var opId = operation.GetProperty("operationId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(opId), $"No operationId: {path.Name}/{method.Name}");
            Assert.True(ids.Add(opId!), $"Duplicate operationId: {opId}");
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            Assert.NotEmpty(operation.GetProperty("tags").EnumerateArray().ToArray());
            Assert.DoesNotContain(operation.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()), tag => tag == "99 Other");
            Assert.DoesNotContain(":", operation.GetProperty("summary").GetString() ?? "");
            if (path.Name is not ("/api/auth/login" or "/api/auth/refresh" or "/health/live") && !path.Name.StartsWith("/api/onboarding/", StringComparison.Ordinal))
            {
                Assert.NotEmpty(operation.GetProperty("security").EnumerateArray().ToArray());
                Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
            }
            operations++;
        }
        Assert.True(operations >= 11, $"Expected at least 11 controller operations, got {operations}");
        AssertScheme(paths, "/api/auth/login", "post", null);
        AssertScheme(paths, "/api/onboarding/register", "post", null);
        AssertScheme(paths, "/api/onboarding/confirm", "post", null);
        AssertScheme(paths, "/api/onboarding/resend-confirmation", "post", null);
        AssertScheme(paths, "/api/onboarding/forgot-password", "post", null);
        AssertScheme(paths, "/api/onboarding/reset-password", "post", null);
        AssertScheme(paths, "/api/auth/refresh", "post", null);
        AssertScheme(paths, "/api/auth/me", "get", "LocalJwt");
        AssertScheme(paths, "/api/auth/external/me", "get", "ExternalOidc");
        AssertScheme(paths, "/api/auth/opaque/me", "get", "OpaqueToken");
        AssertScheme(paths, "/api/auth/external-links/challenge", "post", "LocalJwt");
        AssertScheme(paths, "/api/auth/external-links/complete", "post", "ExternalOidc");
        AssertScheme(paths, "/api/auth/external-links/me", "get", "ExternalOidc");
        AssertScheme(paths, "/api/auth/external-links", "delete", "LocalJwt");
        AssertScheme(paths, "/health/live", "get", null);
        AssertScheme(paths, "/api/api-keys", "post", "LocalJwt");
        AssertScheme(paths, "/api/users", "get", "LocalJwt");
        AssertScheme(paths, "/api/integrations/ping", "get", "ApiKey");
        Assert.True(paths.GetProperty("/api/auth/login").GetProperty("post").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(paths.GetProperty("/api/api-keys").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
    }

    [Fact]
    public async Task SwaggerPortalHasARealBundledStylesheet()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var ui = await client.GetStringAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        Assert.Contains("swagger", ui, StringComparison.OrdinalIgnoreCase);
        Assert.True(ui.Contains("/swagger-ui/custom.css"), $"CSS injection not in UI, HTML: {ui[..Math.Min(ui.Length, 8000)]}");
        using var response = await client.GetAsync("/swagger-ui/custom.css", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stylesheet = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("--mkx-bg", stylesheet);
        Assert.Contains("@media", stylesheet);
    }

    [Fact]
    public async Task ProductionDoesNotExposeSwaggerOrThemedStaticFiles()
    {
        using var factory = Factory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var json = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var css = await client.GetAsync("/swagger-ui/custom.css", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.OK, json.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, css.StatusCode);
    }

    private static void AssertScheme(JsonElement paths, string path, string method, string? scheme)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        if (scheme is null)
        {
            if (operation.TryGetProperty("security", out var anonymous))
                Assert.Empty(anonymous.EnumerateArray().ToArray());
            return;
        }
        var security = operation.GetProperty("security");
        Assert.True(security.EnumerateArray().Any(entry => entry.TryGetProperty(scheme, out _)), $"{path} expects {scheme}, got {security.GetRawText()}");
        Assert.Single(security.EnumerateArray().ToArray());
    }
}
