using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Name)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();

        builder.Property(organization => organization.NormalizedName)
            .HasMaxLength(Organization.NameMaxLength)
            .IsRequired();

        builder.Property(organization => organization.Description)
            .HasMaxLength(Organization.DescriptionMaxLength);

        builder.Property(organization => organization.OwnerUserId)
            .IsRequired();

        builder.Property(organization => organization.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(organization => organization.PolicyVersion)
            .IsRequired();

        builder.Property(organization => organization.CreatedAtUtc)
            .IsRequired();

        builder.Property(organization => organization.UpdatedAtUtc)
            .IsRequired();

        builder.Property(organization => organization.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(organization => organization.OwnerUserId);
        builder.HasIndex(organization => organization.NormalizedName);
    }
}
