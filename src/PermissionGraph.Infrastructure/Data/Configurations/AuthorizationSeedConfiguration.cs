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

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", table =>
        {
            table.HasCheckConstraint(
                "CK_Roles_RoleType",
                "\"RoleType\" IN ('System', 'Custom')");
            table.HasCheckConstraint(
                "CK_Roles_ScopeType",
                "\"ScopeType\" IN ('Organization', 'Project')");
            table.HasCheckConstraint(
                "CK_Roles_Lifecycle",
                "((\"IsActive\" = TRUE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"ArchivedAtUtc\" IS NOT NULL))");
        });

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(Role.NameMaxLength)
            .IsRequired();

        builder.Property(role => role.NormalizedName)
            .HasMaxLength(Role.NameMaxLength)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasMaxLength(Role.DescriptionMaxLength);

        builder.Property(role => role.ScopeType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(role => role.RoleType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(role => role.CreatedAtUtc)
            .IsRequired();

        builder.Property(role => role.UpdatedAtUtc)
            .IsRequired();

        builder.Property(role => role.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasMany<RolePermission>("_permissions")
            .WithOne()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.ClientCascade);

        builder.Navigation(role => role.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(role => new { role.OrganizationId, role.ScopeType, role.NormalizedName })
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");
        builder.HasIndex(role => new { role.OrganizationId, role.ScopeType, role.IsActive });
        builder.HasIndex(role => new { role.OrganizationId, role.NormalizedName });
        builder.HasAlternateKey(role => new { role.Id, role.OrganizationId });
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });
        builder.Property(rolePermission => rolePermission.AddedAtUtc).IsRequired();
        builder.Property(rolePermission => rolePermission.AddedByUserId).IsRequired();
        builder.HasIndex(rolePermission => new { rolePermission.PermissionId, rolePermission.RoleId });

        builder.HasOne<PermissionDefinition>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
