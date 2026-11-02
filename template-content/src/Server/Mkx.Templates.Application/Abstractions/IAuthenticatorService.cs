using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Abstractions;

public interface IAuthenticatorService
{
    Task<AuthenticatorStatusResponse?> StatusAsync(Guid userId, CancellationToken ct);
    Task<AuthenticatorSetupResponse?> BeginSetupAsync(Guid userId, string password, CancellationToken ct);
    Task<AuthenticatorEnableResponse?> EnableAsync(Guid userId, string code, CancellationToken ct);
    Task<bool> DisableAsync(Guid userId, AuthenticatorDisableRequest request, CancellationToken ct);
}
