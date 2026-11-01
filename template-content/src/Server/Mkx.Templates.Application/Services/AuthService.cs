using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> users,
    IAccessTokenIssuer tokens,
    TimeProvider clock) : IAuthService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.Username);
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled || await users.IsLockedOutAsync(user))
            return null;

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        return await CreateSessionAsync(user, Guid.NewGuid(), ct);
    }

    public async Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = Hash(request.RefreshToken);
        var session = await db.RefreshSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (session is null)
            return null;

        var now = clock.GetUtcNow();
        if (session.RevokedAt is not null)
        {
            // Replaying a rotated token invalidates every active token in its family.
            if (session.ReplacedByHash is not null)
                await RevokeFamilyAsync(session.FamilyId, now, ct);

            return null;
        }

        if (session.ExpiresAt <= now)
            return null;

        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled || await users.IsLockedOutAsync(user))
            return null;

        if (!string.Equals(await users.GetSecurityStampAsync(user), session.SecurityStamp, StringComparison.Ordinal))
            return null;

        // SQL Server's retry strategy requires the entire transaction inside the retriable delegate.
        var rotated = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retry must not carry over tracked entities from the previous failed attempt.
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var replacement = NewRefreshToken();
            var replacementHash = Hash(replacement);
            var updated = await db.RefreshSessions
                .Where(x => x.Id == session.Id && x.RevokedAt == null && x.ExpiresAt > now)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.RevokedAt, now)
                    .SetProperty(x => x.ReplacedByHash, replacementHash), ct);

            if (updated != 1)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var expires = now.Add(RefreshLifetime);
            db.RefreshSessions.Add(new RefreshSession
            {
                FamilyId = session.FamilyId,
                UserId = user.Id,
                TokenHash = replacementHash,
                SecurityStamp = session.SecurityStamp,
                CreatedAt = now,
                ExpiresAt = expires
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var roles = await users.GetRolesAsync(user);
            var (access, accessExpires) = tokens.Issue(user, roles.ToArray());
            return new TokenResponse(access, replacement, accessExpires, expires);
        });

        if (rotated is null)
        {
            // Two requests can both read the token before the first finishes rotation.
            // The transaction has been disposed before this query/revocation.
            var current = await db.RefreshSessions.AsNoTracking()
                .Where(x => x.Id == session.Id)
                .Select(x => new { x.RevokedAt, x.ReplacedByHash })
                .SingleOrDefaultAsync(ct);
            if (current?.RevokedAt is not null && current.ReplacedByHash is not null)
                await RevokeFamilyAsync(session.FamilyId, now, ct);
        }

        return rotated;
    }

    public async Task<bool> RevokeAsync(Guid userId, RevokeRequest request, CancellationToken ct)
    {
        var hash = Hash(request.RefreshToken);
        var now = clock.GetUtcNow();
        return await db.RefreshSessions
            .Where(x => x.UserId == userId && x.TokenHash == hash && x.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.RevokedAt, now), ct) == 1;
    }

    public Task<int> RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshSessions.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.RevokedAt, clock.GetUtcNow()), ct);

    public async Task<AccountResponse?> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;
        var roles = await users.GetRolesAsync(user);
        return new AccountResponse(user.Id, user.UserName ?? "", user.DisplayName, roles.ToArray());
    }

    private async Task<TokenResponse> CreateSessionAsync(AppUser user, Guid family, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var refresh = NewRefreshToken();
        var expiration = now.Add(RefreshLifetime);
        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = user.Id,
            FamilyId = family,
            TokenHash = Hash(refresh),
            SecurityStamp = await users.GetSecurityStampAsync(user),
            CreatedAt = now,
            ExpiresAt = expiration
        });
        await db.SaveChangesAsync(ct);

        var roles = await users.GetRolesAsync(user);
        var (access, accessExpires) = tokens.Issue(user, roles.ToArray());
        return new TokenResponse(access, refresh, accessExpires, expiration);
    }

    private Task<int> RevokeFamilyAsync(Guid family, DateTimeOffset when, CancellationToken ct) =>
        db.RefreshSessions.Where(x => x.FamilyId == family && x.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.RevokedAt, when), ct);

    private static string NewRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
