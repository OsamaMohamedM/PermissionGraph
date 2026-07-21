namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class ProjectAdministratorAssignmentConfiguration : IEntityTypeConfiguration<ProjectAdministratorAssignmentRecord>
{
    public void Configure(EntityTypeBuilder<ProjectAdministratorAssignmentRecord> builder)
    {
        builder.ToTable("ProjectAdministratorAssignments");
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.OrganizationId).IsRequired();
        builder.Property(assignment => assignment.ProjectId).IsRequired();
        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.RoleId).IsRequired();
        builder.Property(assignment => assignment.CreatedAtUtc).IsRequired();
        builder.Property(assignment => assignment.CreatedByUserId).IsRequired();

        builder.HasIndex(assignment => new
            {
                assignment.OrganizationId,
                assignment.ProjectId,
                assignment.UserId,
                assignment.RoleId
            })
            .IsUnique();

        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.UserId });
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.ProjectId });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.ProjectId, assignment.OrganizationId })
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.RoleId, assignment.OrganizationId })
            .HasPrincipalKey(role => new { role.Id, role.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}