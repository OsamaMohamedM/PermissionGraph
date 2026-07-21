namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class PermissionDefinitionConfiguration : IEntityTypeConfiguration<PermissionDefinition>
{
    public void Configure(EntityTypeBuilder<PermissionDefinition> builder)
    {
        builder.ToTable("PermissionDefinitions", table =>
        {
            table.HasCheckConstraint(
                "CK_PermissionDefinitions_PermissionType",
                "\"PermissionType\" IN ('Platform', 'Custom')");
            table.HasCheckConstraint(
                "CK_PermissionDefinitions_AllowedScopes",
                "\"AllowedScopes\" IN ('Organization', 'Project', 'OrganizationAndProject')");
            table.HasCheckConstraint(
                "CK_PermissionDefinitions_TypeOrganization",
                "((\"PermissionType\" = 'Platform' AND \"OrganizationId\" IS NULL) OR (\"PermissionType\" = 'Custom' AND \"OrganizationId\" IS NOT NULL))");
            table.HasCheckConstraint(
                "CK_PermissionDefinitions_Lifecycle",
                "((\"IsActive\" = TRUE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"ArchivedAtUtc\" IS NOT NULL))");
            table.HasCheckConstraint(
                "CK_PermissionDefinitions_CustomKeyPrefix",
                "(\"PermissionType\" <> 'Custom' OR \"Key\" NOT LIKE 'pg.%')");
        });

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Key)
            .HasMaxLength(PermissionDefinition.KeyMaxLength)
            .IsRequired();

        builder.Property(permission => permission.NormalizedKey)
            .HasMaxLength(PermissionDefinition.KeyMaxLength)
            .IsRequired();

        builder.Property(permission => permission.DisplayName)
            .HasMaxLength(PermissionDefinition.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasMaxLength(PermissionDefinition.DescriptionMaxLength);

        builder.Property(permission => permission.Module)
            .HasMaxLength(PermissionDefinition.ModuleMaxLength)
            .IsRequired();

        builder.Property(permission => permission.PermissionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(permission => permission.AllowedScopes)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(permission => permission.CreatedAtUtc)
            .IsRequired();

        builder.Property(permission => permission.UpdatedAtUtc)
            .IsRequired();

        builder.Property(permission => permission.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(permission => permission.NormalizedKey)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL");

        builder.HasIndex(permission => new { permission.OrganizationId, permission.NormalizedKey })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NOT NULL");

        builder.HasIndex(permission => new { permission.OrganizationId, permission.IsActive, permission.Id });
        builder.HasIndex(permission => new { permission.Module, permission.IsActive });
        builder.HasIndex(permission => new { permission.PermissionType, permission.IsActive });
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