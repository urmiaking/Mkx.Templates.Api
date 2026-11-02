using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Abstractions;

public interface IExternalAccountLinkService
{
    Task<ExternalLinkChallengeResponse?> StartAsync(Guid localUserId, CancellationToken ct);
    Task<bool> CompleteAsync(string verifiedIssuer, string verifiedSubject, string challenge, CancellationToken ct);
    Task<ExternalLinkedAccountResponse?> ResolveAsync(string verifiedIssuer, string verifiedSubject, CancellationToken ct);
    Task<bool> UnlinkAsync(Guid localUserId, string issuer, CancellationToken ct);
}
