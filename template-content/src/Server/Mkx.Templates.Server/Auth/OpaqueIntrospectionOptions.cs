using System.ComponentModel.DataAnnotations;

namespace Mkx.Templates.Server.Auth;

/// <summary>
/// Disabled by default. All enabled values, including the client secret, come from configuration.
/// The secret must be supplied by environment variables or a secret manager, never committed.
/// </summary>
public sealed class OpaqueIntrospectionOptions
{
    public const string Section = "OpaqueIntrospection";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool IsValid()
    {
        if (!Enabled) return true;
        return IsHttps(Endpoint) && IsHttps(Issuer) &&
               !string.IsNullOrWhiteSpace(Audience) &&
               !string.IsNullOrWhiteSpace(ClientId) &&
               !string.IsNullOrWhiteSpace(ClientSecret);
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Fragment);
}
