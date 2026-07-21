namespace PermissionGraph.Infrastructure.Data;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? ActorUserId { get; set; }

    public required string ActorType { get; set; }

    public required string Action { get; set; }

    public required string TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? ScopeType { get; set; }

    public Guid? ScopeId { get; set; }

    public required string Result { get; set; }

    public string? ReasonCode { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}