using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Domain.Permissions;
using PermissionGraph.Domain.Projects;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.AuthorizationSeed;
using PermissionGraph.Infrastructure.Projects;

namespace PermissionGraph.Infrastructure.Data;

public sealed class PermissionGraphDbContext(DbContextOptions<PermissionGraphDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectAdministratorAssignmentRecord> ProjectAdministratorAssignments => Set<ProjectAdministratorAssignmentRecord>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();

    public DbSet<RoleRecord> Roles => Set<RoleRecord>();

    public DbSet<RolePermissionRecord> RolePermissions => Set<RolePermissionRecord>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyInfrastructureManagedVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PermissionGraphDbContext).Assembly);

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

        builder.Entity<Organization>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(organization => organization.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationMembership>(entity =>
        {
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(membership => membership.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Project>(entity =>
        {
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(project => project.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectAdministratorAssignmentRecord>(entity =>
        {
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(assignment => assignment.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PermissionDefinition>(entity =>
        {
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(permission => permission.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoleRecord>(entity =>
        {
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(role => role.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasAlternateKey(role => new { role.Id, role.OrganizationId });
        });

        builder.Entity<RolePermissionRecord>(entity =>
        {
            entity.HasOne<RoleRecord>()
                .WithMany()
                .HasForeignKey(rolePermission => rolePermission.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PermissionDefinition>()
                .WithMany()
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ApplyInfrastructureManagedVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Organization>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(organization => organization.Version).CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                var original = entry.Property(organization => organization.Version).OriginalValue;
                entry.Property(organization => organization.Version).CurrentValue = original + 1;
            }
        }

        foreach (var entry in ChangeTracker.Entries<OrganizationMembership>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(membership => membership.Version).CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                var original = entry.Property(membership => membership.Version).OriginalValue;
                entry.Property(membership => membership.Version).CurrentValue = original + 1;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Project>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(project => project.Version).CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                var original = entry.Property(project => project.Version).OriginalValue;
                entry.Property(project => project.Version).CurrentValue = original + 1;
            }
        }

        foreach (var entry in ChangeTracker.Entries<PermissionDefinition>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(permission => permission.Version).CurrentValue = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                var original = entry.Property(permission => permission.Version).OriginalValue;
                entry.Property(permission => permission.Version).CurrentValue = original + 1;
            }
        }
    }
}
