using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Infrastructure.Persistence;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<ApiClientKey> ApiClientKeys => Set<ApiClientKey>();
    public DbSet<ExternalIdentityLink> ExternalIdentityLinks => Set<ExternalIdentityLink>();
    public DbSet<ExternalLinkChallenge> ExternalLinkChallenges => Set<ExternalLinkChallenge>();
    public DbSet<RedeemedAuthenticatorCode> RedeemedAuthenticatorCodes => Set<RedeemedAuthenticatorCode>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>(b => { b.Property(x => x.DisplayName).HasMaxLength(160); });
        builder.Entity<RedeemedAuthenticatorCode>(b =>
        {
            b.HasKey(x => new { x.UserId, x.Digest });
            b.Property(x => x.Digest).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.ExpiresAtUtc);
            b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ExternalIdentityLink>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Issuer).HasMaxLength(256).IsRequired();
            b.Property(x => x.Subject).HasMaxLength(256).IsRequired();
            b.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.Issuer }).IsUnique();
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ExternalLinkChallenge>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.ExpiresAt);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ApiClientKey>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Prefix).HasMaxLength(12).IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.Scopes).HasMaxLength(300).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.Prefix);
            b.HasIndex(x => x.CreatedById);
            b.HasIndex(x => x.ExpiresAt);
        });        builder.Entity<RefreshSession>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.FamilyId, x.RevokedAt });
            b.Property(x => x.SecurityStamp).HasMaxLength(128);
            b.Property(x => x.ReplacedByHash).HasMaxLength(64);
            b.HasOne(x => x.User).WithMany(x => x.Sessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
