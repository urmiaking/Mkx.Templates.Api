using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mkx.Templates.Application.Abstractions;

namespace Mkx.Templates.Server.Auth;

/// <summary>Separate service-to-service scheme. Never authenticates a local Identity user.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyService apiKeys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var supplied))
            return AuthenticateResult.NoResult();

        if (supplied.Count != 1 || string.IsNullOrWhiteSpace(supplied[0]))
            return AuthenticateResult.Fail("Invalid API credential.");

        var key = await apiKeys.ValidateAsync(supplied[0]!, Context.RequestAborted);
        if (key is null)
            return AuthenticateResult.Fail("Invalid API credential.");

        var claims = new List<Claim>
        {
            new("api_key_id", key.Id.ToString()),
            new("client_name", key.Name)
        };
        claims.AddRange(key.Scopes.Select(scope => new Claim("scope", scope)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
