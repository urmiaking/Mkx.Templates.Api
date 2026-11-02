using Mkx.Templates.Shared.Contracts;
namespace Mkx.Templates.Application.Abstractions;
public interface IAccountLifecycleService
{
    Task<bool> RegisterAsync(RegisterAccountRequest request, CancellationToken ct);
    Task<bool> ConfirmAsync(ConfirmAccountRequest request, CancellationToken ct);
    Task RequestConfirmationAsync(AccountRecoveryRequest request, CancellationToken ct);
    Task RequestPasswordResetAsync(AccountRecoveryRequest request, CancellationToken ct);
    Task<bool> ResetPasswordAsync(ResetAccountPasswordRequest request, CancellationToken ct);
}
public interface IAccountMessageSender
{
    Task SendAsync(string recipient, string subject, string message, CancellationToken ct);
}
