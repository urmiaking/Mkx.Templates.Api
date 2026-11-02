namespace Mkx.Templates.Domain.Identity;

/// <summary>Persisted metadata for a service-to-service credential. Raw secret is never stored.</summary>
public sealed class ApiClientKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
