using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

/// <summary>
/// WebAuthn via the .NET 10 Identity handler. Temporary attestation/assertion state is
/// held ONLY on the trusted server, bound to a purpose, an identity (for enrollment),
/// a security stamp and a short deadline, then consumed atomically.
/// </summary>
public sealed class PasskeyService(
    UserManager<AppUser> users,
    IPasskeyHandler<AppUser> handler,
    AppDbContext db,
    IAuthenticatorReplayGuard totpReplay,
    AuthService sessions,
    TimeProvider clock) : IPasskeyService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    public async Task<PasskeyCeremonyResponse?> BeginRegistrationAsync(
        Guid userId, PasskeyEnrollmentRequest request, HttpContext http, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (!await EligibleAsync(user) || !await users.CheckPasswordAsync(user!, request.Password) ||
            !await SecondFactorAsync(user!, request.AuthenticatorCode, request.RecoveryCode, ct))
            return null;

        var options = await handler.MakeCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = userId.ToString(),
            Name = user!.UserName ?? "",
            DisplayName = user.DisplayName
        }, http);

        var ceremony = await SaveAsync("register", options.AttestationState, user, ct);
        return new PasskeyCeremonyResponse(ceremony, options.CreationOptionsJson);
    }

    public async Task<bool> FinishRegistrationAsync(
        Guid userId, PasskeyCompletionRequest request, HttpContext http, CancellationToken ct)
    {
        var ceremony = await ConsumeAsync(request.CeremonyId, "register", userId, ct);
        if (ceremony is null)
            return false;

        var user = await users.FindByIdAsync(userId.ToString());
        if (!await EligibleAsync(user) ||
            !string.Equals(await users.GetSecurityStampAsync(user!), ceremony.SecurityStamp,
                StringComparison.Ordinal))
            return false;

        var result = await handler.PerformAttestationAsync(new PasskeyAttestationContext
        {
            HttpContext = http,
            CredentialJson = request.CredentialJson,
            AttestationState = ceremony.State
        });
        if (!result.Succeeded || !string.Equals(result.UserEntity.Id, userId.ToString(), StringComparison.Ordinal))
            return false;

        return (await users.AddOrUpdatePasskeyAsync(user!, result.Passkey)).Succeeded;
    }

    public async Task<PasskeyCeremonyResponse> BeginLoginAsync(HttpContext http, CancellationToken ct)
    {
        // Discoverable credentials: no username enumeration or user-specific allowCredentials.
        var options = await handler.MakeRequestOptionsAsync(null, http);
        var id = await SaveAsync("login", options.AssertionState, null, ct);
        return new PasskeyCeremonyResponse(id, options.RequestOptionsJson);
    }

    public async Task<TokenResponse?> FinishLoginAsync(
        PasskeyCompletionRequest request, HttpContext http, CancellationToken ct)
    {
        var ceremony = await ConsumeAsync(request.CeremonyId, "login", null, ct);
        if (ceremony is null)
            return null;

        var result = await handler.PerformAssertionAsync(new PasskeyAssertionContext
        {
            HttpContext = http,
            CredentialJson = request.CredentialJson,
            AssertionState = ceremony.State
        });
        if (!result.Succeeded)
            return null;

        var user = result.User;
        if (!await EligibleAsync(user))
            return null;

        // Passkeys are a primary credential. If local MFA is enabled they must NOT
        // silently bypass the existing local second-factor policy.
        if (!await SecondFactorAsync(user, request.AuthenticatorCode, request.RecoveryCode, ct))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        // Identity persists updated authenticator sign counters/flags.
        if (!(await users.AddOrUpdatePasskeyAsync(user, result.Passkey)).Succeeded)
            return null;

        return await sessions.IssueVerifiedPasskeySessionAsync(user, ct);
    }

    public async Task<IReadOnlyList<PasskeySummary>?> ListAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed)
            return null;
        var passkeys = await users.GetPasskeysAsync(user);
        return passkeys.Select(x => new PasskeySummary(Convert.ToBase64String(x.CredentialId)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_'), x.Name)).ToArray();
    }

    public async Task<bool> RemoveAsync(Guid userId, PasskeyRemoveRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (!await EligibleAsync(user) ||
            !await users.CheckPasswordAsync(user!, request.Password) ||
            !await SecondFactorAsync(user!, request.AuthenticatorCode, request.RecoveryCode, ct))
            return false;

        byte[] credentialId;
        try
        {
            var text = request.CredentialId.Replace('-', '+').Replace('_', '/');
            credentialId = Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));
        }
        catch (FormatException) { return false; }

        if (await users.GetPasskeyAsync(user!, credentialId) is null)
            return false;
        var removed = await users.RemovePasskeyAsync(user!, credentialId);
        if (!removed.Succeeded)
            return false;
        await users.UpdateSecurityStampAsync(user!);
        await sessions.RevokeAllAsync(userId, ct);
        return true;
    }

    private async Task<bool> EligibleAsync(AppUser? user)
        => user is not null && user.EmailConfirmed && !await users.IsLockedOutAsync(user);

    private async Task<bool> SecondFactorAsync(
        AppUser user, string? code, string? recovery, CancellationToken ct)
    {
        if (!user.TwoFactorEnabled)
            return code is null && recovery is null;
        if (code is not null && recovery is null &&
            await users.VerifyTwoFactorTokenAsync(user, "Authenticator", code))
            return await totpReplay.TryConsumeAsync(user, code, ct);
        return recovery is not null && code is null &&
               (await users.RedeemTwoFactorRecoveryCodeAsync(user, recovery)).Succeeded;
    }

    private async Task<Guid> SaveAsync(string purpose, string? state, AppUser? user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length > 16000)
            throw new InvalidOperationException("WebAuthn handler returned invalid ceremony state.");
        var now = clock.GetUtcNow().UtcDateTime;
        // Purge expired and abandoned ceremonies after a short retention window.
        await db.PasskeyCeremonies.Where(x => x.ExpiresAtUtc < now.AddMinutes(-10))
            .ExecuteDeleteAsync(ct);
        var item = new PasskeyCeremony
        {
            Purpose = purpose,
            UserId = user?.Id,
            SecurityStamp = user is null ? null : await users.GetSecurityStampAsync(user),
            State = state,
            ExpiresAtUtc = now.Add(Lifetime)
        };
        db.PasskeyCeremonies.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    private async Task<PasskeyCeremony?> ConsumeAsync(
        Guid id, string purpose, Guid? userId, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var ceremony = await db.PasskeyCeremonies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.Purpose == purpose &&
                x.UserId == userId && x.ConsumedAtUtc == null, ct);
        if (ceremony is null || ceremony.ExpiresAtUtc <= now)
            return null;

        // A single atomic update fences out all parallel submissions for this challenge.
        var updated = await db.PasskeyCeremonies
            .Where(x => x.Id == id && x.Purpose == purpose && x.UserId == userId &&
                x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
        return updated == 1 ? ceremony : null;
    }
}
