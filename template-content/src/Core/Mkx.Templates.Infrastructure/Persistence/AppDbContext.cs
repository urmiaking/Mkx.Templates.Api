using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mkx.Templates.Domain.Identity;
namespace Mkx.Templates.Infrastructure.Persistence;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>(b => { b.Property(x => x.DisplayName).HasMaxLength(160); });
        builder.Entity<RefreshSession>(b =>
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
