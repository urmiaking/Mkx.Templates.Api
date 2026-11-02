using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;

namespace Mkx.Templates.Application.Services;

/// <summary>Persisted single-use TOTP fence shared across distributed API instances.</summary>
public sealed class AuthenticatorReplayGuard(
    AppDbContext db, UserManager<AppUser> users, TimeProvider clock) : IAuthenticatorReplayGuard
{
    public async Task<bool> TryConsumeAsync(AppUser user, string code, CancellationToken ct)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            return false;

        var secret = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        // The low-entropy raw code is never stored. Digest is bound to the
        // authenticator secret and user ID, so different accounts cannot collide.
        var digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(user.Id + ":" + secret + ":" + code)));
        var now = clock.GetUtcNow().UtcDateTime;

        await db.RedeemedAuthenticatorCodes
            .Where(x => x.UserId == user.Id && x.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(ct);

        var fence = new RedeemedAuthenticatorCode
        {
            UserId = user.Id,
            Digest = digest,
            ExpiresAtUtc = now.AddMinutes(3)
        };
        db.RedeemedAuthenticatorCodes.Add(fence);
        try
        {
            await db.SaveChangesAsync(ct);
            db.Entry(fence).State = EntityState.Detached;
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(fence).State = EntityState.Detached;
            return false;
        }
    }
}
