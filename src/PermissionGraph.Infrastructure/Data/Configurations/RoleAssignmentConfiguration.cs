namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments", table =>
        {
            table.HasCheckConstraint(
                "CK_RoleAssignments_Status",
                "\"Status\" IN ('Scheduled', 'Active', 'Revoked', 'Expired')");
            table.HasCheckConstraint(
                "CK_RoleAssignments_ScopeType",
                "\"ScopeType\" IN ('Organization', 'Project')");
            table.HasCheckConstraint(
                "CK_RoleAssignments_ExpirationAfterStart",
                "\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"StartsAtUtc\"");
            table.HasCheckConstraint(
                "CK_RoleAssignments_OrganizationScopeId",
                "\"ScopeType\" <> 'Organization' OR \"ScopeId\" = \"OrganizationId\"");
        });

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.OrganizationId).IsRequired();
        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.RoleId).IsRequired();
        builder.Property(assignment => assignment.ScopeType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(assignment => assignment.ScopeId).IsRequired();
        builder.Property(assignment => assignment.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(assignment => assignment.StartsAtUtc).IsRequired();
        builder.Property(assignment => assignment.GrantedByUserId).IsRequired();
        builder.Property(assignment => assignment.GrantReason)
            .HasMaxLength(RoleAssignment.ReasonMaxLength)
            .IsRequired();
        builder.Property(assignment => assignment.RevokeReason)
            .HasMaxLength(RoleAssignment.ReasonMaxLength);
        builder.Property(assignment => assignment.CreatedAtUtc).IsRequired();
        builder.Property(assignment => assignment.UpdatedAtUtc).IsRequired();
        builder.Property(assignment => assignment.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(assignment => new
            {
                assignment.UserId,
                assignment.RoleId,
                assignment.ScopeType,
                assignment.ScopeId
            })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Scheduled', 'Active')");

        builder.HasIndex(assignment => new
        {
            assignment.OrganizationId,
            assignment.UserId,
            assignment.ScopeType,
            assignment.ScopeId,
            assignment.Status
        });
        builder.HasIndex(assignment => new
        {
            assignment.UserId,
            assignment.StartsAtUtc,
            assignment.ExpiresAtUtc,
            assignment.Status
        });
        builder.HasIndex(assignment => new { assignment.ExpiresAtUtc, assignment.Status });
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.Status });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(assignment => assignment.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.RoleId, assignment.OrganizationId })
            .HasPrincipalKey(role => new { role.Id, role.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
