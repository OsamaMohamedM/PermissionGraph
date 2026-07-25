namespace PermissionGraph.Application.Features.Authorization.ExplainAccess.Models;

public sealed record ExplainAccessResult(
    bool Allowed,
    string ReasonCode,
    DateTimeOffset EvaluatedAtUtc,
    Guid ActorUserId,
    Guid SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string PermissionKey,
    AuthorizationScopeType ScopeType,
    string Summary,
    IReadOnlyList<AccessExplanationStepResult> Steps,
    AccessExplanationPathResult? MatchedPath);

public sealed record AccessExplanationStepResult(
    int Order,
    string Code,
    string Status,
    string Message,
    IReadOnlyDictionary<string, string> Details);

public sealed record AccessExplanationPathResult(
    string Type,
    Guid? AssignmentId,
    Guid? RoleId,
    string? RoleName,
    string ScopeType,
    Guid ScopeId,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public static class AccessExplanationStepStatus
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string Info = "Info";
}
