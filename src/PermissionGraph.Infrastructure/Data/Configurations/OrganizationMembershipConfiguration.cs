using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.OrganizationId)
            .IsRequired();

        builder.Property(membership => membership.UserId)
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(membership => membership.JoinedAtUtc)
            .IsRequired();

        builder.Property(membership => membership.AuthorizationVersion)
            .IsRequired();

        builder.Property(membership => membership.CreatedAtUtc)
            .IsRequired();

        builder.Property(membership => membership.UpdatedAtUtc)
            .IsRequired();

        builder.Property(membership => membership.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId })
            .IsUnique();

        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId, membership.Status });
        builder.HasIndex(membership => new { membership.UserId, membership.Status });
    }
}
