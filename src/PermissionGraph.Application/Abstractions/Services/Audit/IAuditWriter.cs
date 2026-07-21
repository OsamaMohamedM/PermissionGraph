namespace PermissionGraph.Application.Abstractions.Services.Audit;

public interface IAuditWriter
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}

public sealed record AuditRecord(
    Guid? OrganizationId,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    Guid? TargetId,
    string Result,
    DateTimeOffset OccurredAtUtc);