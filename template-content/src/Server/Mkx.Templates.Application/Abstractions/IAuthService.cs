using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Shared.Contracts;
namespace Mkx.Templates.Application.Abstractions;
public interface IAccessTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(AppUser user, IReadOnlyList<string> roles);
}
public interface IAuthService
{
    Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task<bool> RevokeAsync(Guid userId, RevokeRequest request, CancellationToken ct);
    Task<int> RevokeAllAsync(Guid userId, CancellationToken ct);
    Task<AccountResponse?> GetMeAsync(Guid userId, CancellationToken ct);
}
