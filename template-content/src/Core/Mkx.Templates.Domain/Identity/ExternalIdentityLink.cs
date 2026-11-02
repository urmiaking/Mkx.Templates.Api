namespace Mkx.Templates.Domain.Identity;

public sealed class ExternalIdentityLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class ExternalLinkChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
