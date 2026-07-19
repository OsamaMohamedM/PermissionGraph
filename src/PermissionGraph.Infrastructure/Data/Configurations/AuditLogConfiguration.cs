using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PermissionGraph.Infrastructure.Data.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.ActorType).HasMaxLength(32).IsRequired();
        builder.Property(audit => audit.Action).HasMaxLength(160).IsRequired();
        builder.Property(audit => audit.TargetType).HasMaxLength(120).IsRequired();
        builder.Property(audit => audit.ScopeType).HasMaxLength(32);
        builder.Property(audit => audit.Result).HasMaxLength(32).IsRequired();
        builder.Property(audit => audit.ReasonCode).HasMaxLength(120);
        builder.Property(audit => audit.CorrelationId).HasMaxLength(128);
        builder.Property(audit => audit.OccurredAtUtc).IsRequired();

        builder.HasIndex(audit => new { audit.OrganizationId, audit.OccurredAtUtc, audit.Id });
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAtUtc });
        builder.HasIndex(audit => new { audit.TargetType, audit.TargetId, audit.OccurredAtUtc });
    }
}
