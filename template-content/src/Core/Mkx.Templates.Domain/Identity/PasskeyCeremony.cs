namespace Mkx.Templates.Domain.Identity;

/// <summary>Server-side WebAuthn challenge; no browser-controlled attestation state.</summary>
public sealed class PasskeyCeremony
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string State { get; set; } = "";
    public string? SecurityStamp { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}
