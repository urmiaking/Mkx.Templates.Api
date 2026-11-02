using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

/// <summary>
/// Two-party proof: a signed-in local (non-admin) user receives a one-time secret;
/// a holder of a verified OIDC user access token presents that secret exactly once.
/// No silent username/email mapping and no automatic local administrator privileges.
/// </summary>
public sealed class ExternalAccountLinkService(
    AppDbContext db, UserManager<AppUser> users, TimeProvider clock) : IExternalAccountLinkService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    public async Task<ExternalLinkChallengeResponse?> StartAsync(Guid localUserId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(localUserId.ToString());
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || await users.IsInRoleAsync(user, "Administrator"))
            return null;

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiration = clock.GetUtcNow().Add(ChallengeLifetime);
        db.ExternalLinkChallenges.Add(new ExternalLinkChallenge
        {
            UserId = localUserId,
            TokenHash = Hash(raw),
            ExpiresAt = expiration
        });
        await db.SaveChangesAsync(ct);
        return new ExternalLinkChallengeResponse(raw, expiration);
    }

    public async Task<bool> CompleteAsync(string verifiedIssuer, string verifiedSubject, string challenge, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(verifiedIssuer) || verifiedIssuer.Length > 256 ||
            string.IsNullOrWhiteSpace(verifiedSubject) || verifiedSubject.Length > 256 ||
            challenge.Length is < 30 or > 128)
            return false;

        var tokenHash = Hash(challenge);
        var now = clock.GetUtcNow();

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var proof = await db.ExternalLinkChallenges.AsNoTracking()
                .Where(x => x.TokenHash == tokenHash && x.ConsumedAt == null)
                .Select(x => new { x.Id, x.UserId, x.ExpiresAt })
                .SingleOrDefaultAsync(ct);
            if (proof is null || proof.ExpiresAt <= now)
                return false;

            var user = await users.FindByIdAsync(proof.UserId.ToString());
            if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
                await users.IsLockedOutAsync(user) || await users.IsInRoleAsync(user, "Administrator"))
                return false;

            if (await db.ExternalIdentityLinks.AnyAsync(x =>
                (x.Issuer == verifiedIssuer && x.Subject == verifiedSubject) ||
                (x.Issuer == verifiedIssuer && x.UserId == proof.UserId), ct))
                return false;

            var consumed = await db.ExternalLinkChallenges
                .Where(x => x.Id == proof.Id && x.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, now), ct);
            if (consumed != 1)
                return false;

            db.ExternalIdentityLinks.Add(new ExternalIdentityLink
            {
                UserId = proof.UserId,
                Issuer = verifiedIssuer,
                Subject = verifiedSubject,
                LinkedAt = now
            });
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }
            catch (DbUpdateException error) when (error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                await tx.RollbackAsync(ct);
                return false; // Unique (issuer,subject) and (user,issuer) database constraints.
            }
        });
    }

    public async Task<ExternalLinkedAccountResponse?> ResolveAsync(
        string verifiedIssuer, string verifiedSubject, CancellationToken ct)
    {
        var link = await db.ExternalIdentityLinks.AsNoTracking()
            .Where(x => x.Issuer == verifiedIssuer && x.Subject == verifiedSubject)
            .Select(x => new { x.UserId })
            .SingleOrDefaultAsync(ct);
        if (link is null)
            return null;

        var user = await users.FindByIdAsync(link.UserId.ToString());
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || await users.IsInRoleAsync(user, "Administrator"))
            return null;

        return new ExternalLinkedAccountResponse(user.Id, user.UserName ?? "", user.DisplayName);
    }

    public async Task<bool> UnlinkAsync(Guid localUserId, string issuer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issuer)) return false;
        var removed = await db.ExternalIdentityLinks.Where(x => x.UserId == localUserId && x.Issuer == issuer)
            .ExecuteDeleteAsync(ct);
        await db.ExternalLinkChallenges.Where(x => x.UserId == localUserId).ExecuteDeleteAsync(ct);
        return removed == 1;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
