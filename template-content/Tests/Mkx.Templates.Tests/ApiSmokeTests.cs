using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public async Task ExternalLinkEndpointsRejectAnonymousCalls()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Post, "/api/auth/external-links/challenge"),
            (HttpMethod.Post, "/api/auth/external-links/complete"),
            (HttpMethod.Get, "/api/auth/external-links/me"),
            (HttpMethod.Delete, "/api/auth/external-links")
        })
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
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

    [Fact]
    public void UnannotatedEndpointsRequireAuthenticationByDefault()
    {
        using var factory = CreateFactory();
        var authorization = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        Assert.NotNull(authorization.FallbackPolicy);
        Assert.Contains(authorization.FallbackPolicy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task LoginWithoutCredentialsIsRejectedBeforeDatabaseAccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var payload = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/auth/login", payload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
