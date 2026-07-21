namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);

        builder.Property(project => project.OrganizationId)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasMaxLength(Project.NameMaxLength)
            .IsRequired();

        builder.Property(project => project.NormalizedName)
            .HasMaxLength(Project.NameMaxLength)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasMaxLength(Project.DescriptionMaxLength);

        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(project => project.CreatedAtUtc)
            .IsRequired();

        builder.Property(project => project.UpdatedAtUtc)
            .IsRequired();

        builder.Property(project => project.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(project => new { project.OrganizationId, project.NormalizedName })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        builder.HasIndex(project => new { project.OrganizationId, project.Status });
        builder.HasIndex(project => new { project.OrganizationId, project.Id });

        builder.HasAlternateKey(project => new { project.Id, project.OrganizationId });
    }
}