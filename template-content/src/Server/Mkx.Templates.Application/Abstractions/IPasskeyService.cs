using Microsoft.AspNetCore.Http;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Abstractions;

public interface IPasskeyService
{
    Task<PasskeyCeremonyResponse?> BeginRegistrationAsync(Guid userId, PasskeyEnrollmentRequest request, HttpContext http, CancellationToken ct);
    Task<bool> FinishRegistrationAsync(Guid userId, PasskeyCompletionRequest request, HttpContext http, CancellationToken ct);
    Task<PasskeyCeremonyResponse> BeginLoginAsync(HttpContext http, CancellationToken ct);
    Task<TokenResponse?> FinishLoginAsync(PasskeyCompletionRequest request, HttpContext http, CancellationToken ct);
    Task<IReadOnlyList<PasskeySummary>?> ListAsync(Guid userId, CancellationToken ct);
    Task<bool> RemoveAsync(Guid userId, PasskeyRemoveRequest request, CancellationToken ct);
}
