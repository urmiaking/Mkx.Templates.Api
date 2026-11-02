using Microsoft.AspNetCore.Identity;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

/// <summary>ASP.NET Core Identity standard TOTP enrollment; never exposes a setup key after activation.</summary>
public sealed class AuthenticatorService(
    UserManager<AppUser> users,
    IAuthService auth, IAuthenticatorReplayGuard replay) : IAuthenticatorService
{
    public async Task<AuthenticatorStatusResponse?> StatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed)
            return null;
        var key = await users.GetAuthenticatorKeyAsync(user);
        var count = user.TwoFactorEnabled ? await users.CountRecoveryCodesAsync(user) : 0;
        return new AuthenticatorStatusResponse(user.TwoFactorEnabled, !string.IsNullOrWhiteSpace(key), count);
    }

    public async Task<AuthenticatorSetupResponse?> BeginSetupAsync(
        Guid userId, string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, password))
            return null;

        // Rotate the unconfirmed key on each attempt; older setup codes cease to work.
        if (!(await users.ResetAuthenticatorKeyAsync(user)).Succeeded)
            return null;
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var label = Uri.EscapeDataString(user.UserName ?? "");
        var uri = $"otpauth://totp/Mkx.Templates.Api:{label}?secret={Uri.EscapeDataString(key)}&issuer=Mkx.Templates.Api&digits=6";
        return new AuthenticatorSetupResponse(key, uri);
    }

    public async Task<AuthenticatorEnableResponse?> EnableAsync(
        Guid userId, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) ||
            string.IsNullOrWhiteSpace(await users.GetAuthenticatorKeyAsync(user)) ||
            !await users.VerifyTwoFactorTokenAsync(user, "Authenticator", code))
            return null;

        if (!await replay.TryConsumeAsync(user, code, ct))
            return null;
        if (!(await users.SetTwoFactorEnabledAsync(user, true)).Succeeded)
            return null;
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
            return null;
        await auth.RevokeAllAsync(user.Id, ct);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return codes is null ? null : new AuthenticatorEnableResponse(codes.ToArray());
    }

    public async Task<bool> DisableAsync(Guid userId, AuthenticatorDisableRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed || !user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, request.Password))
            return false;

        var totp = !string.IsNullOrEmpty(request.AuthenticatorCode) &&
                   string.IsNullOrEmpty(request.RecoveryCode) &&
                   await users.VerifyTwoFactorTokenAsync(user, "Authenticator", request.AuthenticatorCode);
        var recovery = !string.IsNullOrEmpty(request.RecoveryCode) &&
                       string.IsNullOrEmpty(request.AuthenticatorCode) &&
                       (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode)).Succeeded;
        if (totp && !await replay.TryConsumeAsync(user, request.AuthenticatorCode!, ct))
            totp = false;
        if (!totp && !recovery)
            return false;

        if (!(await users.SetTwoFactorEnabledAsync(user, false)).Succeeded)
            return false;
        if (!(await users.ResetAuthenticatorKeyAsync(user)).Succeeded ||
            !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            return false;
        await auth.RevokeAllAsync(user.Id, ct);
        return true;
    }
}
