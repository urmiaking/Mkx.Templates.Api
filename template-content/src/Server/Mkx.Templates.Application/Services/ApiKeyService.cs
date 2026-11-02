using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Infrastructure.Persistence;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

/// <summary>
/// API keys are high-entropy bearer secrets. Persist only their SHA-256 hashes and metadata.
/// Scope names are deliberately allowlisted; changes require an explicit review.
/// </summary>
public sealed class ApiKeyService(AppDbContext db, TimeProvider clock) : IApiKeyService
{
    public async Task<CreatedApiKeyResponse?> CreateAsync(
        Guid creatorId, CreateApiKeyRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100 || request.ValidDays is < 1 or > 365 ||
            request.Scopes is null || request.Scopes.Length is < 1 or > 10)
            return null;

        var scopes = request.Scopes.Distinct(StringComparer.Ordinal).ToArray();
        if (scopes.Any(s => !ApiKeyScopes.Allowed.Contains(s)))
            return null;

        var random = RandomNumberGenerator.GetBytes(48);
        var raw = "mkx_" + Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = clock.GetUtcNow();
        var expires = now.AddDays(request.ValidDays);
        var key = new ApiClientKey
        {
            Name = name,
            Prefix = raw[..12],
            TokenHash = Hash(raw),
            Scopes = string.Join(',', scopes),
            CreatedById = creatorId,
            CreatedAt = now,
            ExpiresAt = expires
        };
        db.ApiClientKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return new CreatedApiKeyResponse(key.Id, key.Name, raw, key.Prefix, scopes, expires);
    }

    public async Task<IReadOnlyList<ApiKeySummary>> ListAsync(CancellationToken ct)
    {
        var items = await db.ApiClientKeys.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .ToListAsync(ct);
        return items.Select(x => new ApiKeySummary(x.Id, x.Name, x.Prefix,
            SplitScopes(x.Scopes), x.CreatedAt, x.ExpiresAt, x.RevokedAt)).ToArray();
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.ApiClientKeys.Where(x => x.Id == id && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct) == 1;
    }

    public async Task<ValidatedApiKey?> ValidateAsync(string rawKey, CancellationToken ct)
    {
        // Avoid a DB query for arbitrarily long/malformed credentials.
        if (rawKey.Length != 68 || !rawKey.StartsWith("mkx_", StringComparison.Ordinal))
            return null;

        var tokenHash = Hash(rawKey);
        var now = clock.GetUtcNow();
        var record = await db.ApiClientKeys.AsNoTracking()
            .Where(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now)
            .Select(x => new { x.Id, x.Name, x.Scopes })
            .SingleOrDefaultAsync(ct);
        return record is null ? null : new ValidatedApiKey(record.Id, record.Name, SplitScopes(record.Scopes));
    }

    private static string[] SplitScopes(string raw) => raw.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
