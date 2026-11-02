namespace Mkx.Templates.Domain.Identity;

/// <summary>Persisted TOTP replay fence. Digests are bound to an account and its authenticator key.</summary>
public sealed class RedeemedAuthenticatorCode
{
    public Guid UserId { get; set; }
    public string Digest { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
