using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Application.Abstractions;

public interface IAuthenticatorReplayGuard
{
    Task<bool> TryConsumeAsync(AppUser user, string code, CancellationToken ct);
}
