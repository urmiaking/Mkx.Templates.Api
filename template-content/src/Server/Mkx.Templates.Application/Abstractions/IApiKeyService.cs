using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Abstractions;

public sealed record ValidatedApiKey(Guid Id, string Name, string[] Scopes);

public interface IApiKeyService
{
    Task<CreatedApiKeyResponse?> CreateAsync(Guid creatorId, CreateApiKeyRequest request, CancellationToken ct);
    Task<IReadOnlyList<ApiKeySummary>> ListAsync(CancellationToken ct);
    Task<bool> RevokeAsync(Guid id, CancellationToken ct);
    Task<ValidatedApiKey?> ValidateAsync(string rawKey, CancellationToken ct);
}
