using System.Net;
using System.Net.Http.Headers;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Server.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mkx.Templates.Tests;

public sealed class ExternalOidcTests
{
    private static WebApplicationFactory<Program> CreateFactory(bool enabled = false, string authority = "",
        string audience = "") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = new string('J', 64),
                    ["BootstrapAdmin:Enabled"] = "false",
                    ["ExternalOidc:Enabled"] = enabled.ToString(),
                    ["ExternalOidc:Authority"] = authority,
                    ["ExternalOidc:Audience"] = audience
                }));
        });

    [Fact]
    public async Task ExternalEndpointRejectsAnonymousCallersWhenDisabled()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/auth/external/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExternalEndpointDoesNotTrustLocalBearerToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var localJwt = new JwtTokenIssuer(Options.Create(new JwtOptions
        {
            Issuer = "Mkx.Templates.Api",
            Audience = "Mkx.Templates.Clients",
            SigningKey = new string('J', 64)
        }), TimeProvider.System).Issue(
            new AppUser { Id = Guid.NewGuid(), UserName = "admin" }, ["Administrator"]).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", localJwt);
        using var response = await client.GetAsync("/api/auth/external/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ExternalJwtSchemeUsesHttpsAuthorityAndAudienceWhenEnabled()
    {
        using var factory = CreateFactory(true, "https://login.example.test", "my-api");
        using var client = factory.CreateClient();
        var option = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("ExternalOidc");
        Assert.Equal("https://login.example.test", option.Authority);
        Assert.Equal("my-api", option.Audience);
        Assert.True(option.RequireHttpsMetadata);
        Assert.True(option.TokenValidationParameters.ValidateIssuer);
        Assert.True(option.TokenValidationParameters.ValidateAudience);
    }

    [Fact]
    public void EnabledExternalOidcRejectsInsecureMetadataAuthority()
    {
        using var factory = CreateFactory(true, "http://login.example.test", "my-api");
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
