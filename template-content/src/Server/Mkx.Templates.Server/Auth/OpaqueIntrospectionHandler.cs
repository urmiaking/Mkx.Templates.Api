using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Mkx.Templates.Server.Auth;

/// <summary>
/// OAuth 2.0 RFC 7662 introspection of opaque access tokens. Introspected identities
/// are never local ASP.NET Identity accounts and cannot grant local administrator roles.
/// </summary>
public sealed class OpaqueIntrospectionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemes,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<OpaqueIntrospectionOptions> configuration,
    IHttpClientFactory clients,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemes, logger, encoder)
{
    public const string SchemeName = "OpaqueIntrospection";
    public const string HttpClientName = "opaque-introspection";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = configuration.CurrentValue;
        if (!config.Enabled)
            return AuthenticateResult.NoResult();

        if (!Request.Headers.TryGetValue("Authorization", out var headers) ||
            headers.Count != 1 || !AuthenticationHeaderValue.TryParse(headers[0], out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter) ||
            authorization.Parameter.Length is < 8 or > 8192)
            return AuthenticateResult.NoResult();

        // Do not log token material or the introspection client secret.
        var token = authorization.Parameter;
        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
                ["token_type_hint"] = "access_token"
            })
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ClientId + ":" + config.ClientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await clients.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail("Identity provider did not validate this credential.");

            using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
            using var json = await JsonDocument.ParseAsync(stream,
                new JsonDocumentOptions { MaxDepth = 16 }, Context.RequestAborted);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("active", out var active) ||
                active.ValueKind != JsonValueKind.True)
                return AuthenticateResult.Fail("Inactive access token.");

            if (!ReadString(root, "iss", out var issuer) ||
                !string.Equals(issuer.TrimEnd('/'), config.Issuer.TrimEnd('/'), StringComparison.Ordinal) ||
                !ReadString(root, "sub", out var subject) ||
                !ContainsAudience(root, config.Audience) ||
                !TryUnixTime(root, "exp", out var expiration) ||
                expiration <= clock.GetUtcNow().ToUnixTimeSeconds())
                return AuthenticateResult.Fail("Introspection claims failed verification.");

            if (root.TryGetProperty("nbf", out _) &&
                (!TryUnixTime(root, "nbf", out var notBefore) ||
                 notBefore > clock.GetUtcNow().ToUnixTimeSeconds()))
                return AuthenticateResult.Fail("Access token is not yet valid.");

            var claims = new List<Claim> { new("sub", subject), new("iss", issuer) };
            if (ReadString(root, "scope", out var scopes))
                claims.AddRange(scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(scope => new Claim("scope", scope)));

            var identity = new ClaimsIdentity(claims, SchemeName, "sub", "scope");
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            return AuthenticateResult.Fail("Identity provider introspection unavailable or invalid.");
        }
    }

    private static bool ReadString(JsonElement element, string name, out string value)
    {
        value = "";
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ContainsAudience(JsonElement element, string expected)
    {
        if (!element.TryGetProperty("aud", out var audience))
            return false;

        return audience.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audience.GetString(), expected, StringComparison.Ordinal),
            JsonValueKind.Array => audience.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String &&
                string.Equals(a.GetString(), expected, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool TryUnixTime(JsonElement element, string name, out long seconds)
    {
        seconds = 0;
        return element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out seconds);
    }
}
