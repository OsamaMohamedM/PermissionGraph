namespace PermissionGraph.Infrastructure.Services.Audit;

internal sealed class EfAuditWriter(PermissionGraphDbContext dbContext, IGuidProvider guidProvider) : IAuditWriter
{
    public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = guidProvider.NewGuid(),
            OrganizationId = record.OrganizationId,
            ActorUserId = record.ActorUserId,
            ActorType = record.ActorUserId is null ? "System" : "User",
            Action = record.Action,
            TargetType = record.TargetType,
            TargetId = record.TargetId,
            Result = record.Result,
            OccurredAtUtc = record.OccurredAtUtc
        });

        return Task.CompletedTask;
    }
}