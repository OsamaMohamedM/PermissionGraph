using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionGraph.Infrastructure.AuthorizationSeed;

namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class PermissionDefinitionRecordConfiguration : IEntityTypeConfiguration<PermissionDefinitionRecord>
{
    public void Configure(EntityTypeBuilder<PermissionDefinitionRecord> builder)
    {
        builder.ToTable("PermissionDefinitions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Key).HasMaxLength(120).IsRequired();
        builder.Property(permission => permission.NormalizedKey).HasMaxLength(120).IsRequired();
        builder.Property(permission => permission.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(1000);
        builder.Property(permission => permission.Module).HasMaxLength(80).IsRequired();
        builder.Property(permission => permission.PermissionType).HasMaxLength(32).IsRequired();
        builder.Property(permission => permission.AllowedScopes).HasMaxLength(64).IsRequired();
        builder.HasIndex(permission => permission.NormalizedKey)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL");
        builder.HasIndex(permission => new { permission.OrganizationId, permission.NormalizedKey, permission.IsActive });
    }
}

internal sealed class RoleRecordConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(80).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(80).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(1000);
        builder.Property(role => role.ScopeType).HasMaxLength(32).IsRequired();
        builder.Property(role => role.RoleType).HasMaxLength(32).IsRequired();
        builder.HasIndex(role => new { role.OrganizationId, role.ScopeType, role.NormalizedName })
            .IsUnique()
            .HasFilter("\"IsActive\" = true");
        builder.HasIndex(role => new { role.OrganizationId, role.ScopeType, role.IsActive });
    }
}

internal sealed class RolePermissionRecordConfiguration : IEntityTypeConfiguration<RolePermissionRecord>
{
    public void Configure(EntityTypeBuilder<RolePermissionRecord> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });
        builder.HasIndex(rolePermission => new { rolePermission.PermissionId, rolePermission.RoleId });
    }
}
