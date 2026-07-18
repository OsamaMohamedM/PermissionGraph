using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PermissionGraph.Infrastructure.Authentication;

namespace PermissionGraph.Infrastructure.Data;

public sealed class PermissionGraphDbContext(DbContextOptions<PermissionGraphDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(user => user.CreatedAtUtc)
                .IsRequired();

            entity.Property(user => user.IsActive)
                .IsRequired();

            entity.HasIndex(user => user.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex");
        });

        builder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("RefreshSessions");

            entity.HasKey(session => session.Id);

            entity.Property(session => session.TokenHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(session => session.CreatedByIp)
                .HasMaxLength(64);

            entity.Property(session => session.RevokedByIp)
                .HasMaxLength(64);

            entity.Property(session => session.UserAgentHash)
                .HasMaxLength(128);

            entity.Property(session => session.Version)
                .IsRowVersion();

            entity.HasIndex(session => session.TokenHash)
                .IsUnique();

            entity.HasIndex(session => session.UserId);
            entity.HasIndex(session => session.TokenFamilyId);
            entity.HasIndex(session => session.ExpiresAtUtc);
            entity.HasIndex(session => session.ReplacedBySessionId);

            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
