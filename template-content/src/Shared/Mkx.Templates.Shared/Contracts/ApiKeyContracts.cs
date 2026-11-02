using System.ComponentModel.DataAnnotations;

namespace Mkx.Templates.Shared.Contracts;

public static class ApiKeyScopes
{
    public const string IntegrationRead = "integration:read";
    public const string IntegrationWrite = "integration:write";
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        IntegrationRead, IntegrationWrite
    };
}

public sealed record CreateApiKeyRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required] string[] Scopes,
    [Range(1, 365)] int ValidDays = 90);

public sealed record ApiKeySummary(
    Guid Id, string Name, string Prefix, string[] Scopes,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);

/// <summary>Key is returned exactly once and never persisted in plaintext.</summary>
public sealed record CreatedApiKeyResponse(
    Guid Id, string Name, string ApiKey, string Prefix, string[] Scopes, DateTimeOffset ExpiresAt);
