namespace PermissionGraph.Contracts.Authorization;

public sealed record AuthorizationCheckRequest(
    Guid? SubjectUserId,
    Guid? ProjectId,
    string PermissionKey);

public sealed record AuthorizationBatchCheckRequest(
    IReadOnlyList<AuthorizationBatchCheckItemRequest> Checks);

public sealed record AuthorizationBatchCheckItemRequest(
    string CorrelationId,
    Guid? SubjectUserId,
    Guid? ProjectId,
    string PermissionKey);

public sealed record AuthorizationDecisionResponse(
    bool Allowed,
    string ReasonCode,
    DateTimeOffset EvaluatedAtUtc);

public sealed record AuthorizationBatchCheckResponse(
    IReadOnlyList<AuthorizationBatchDecisionResponse> Items);

public sealed record AuthorizationBatchDecisionResponse(
    string CorrelationId,
    int Index,
    AuthorizationDecisionResponse Decision);

public sealed record ExplainAccessRequest(
    Guid? SubjectUserId,
    Guid? ProjectId,
    string PermissionKey,
    DateTimeOffset? EvaluatedAtUtc);

public sealed record ExplainAccessResponse(
    bool Allowed,
    string ReasonCode,
    DateTimeOffset EvaluatedAtUtc,
    Guid ActorUserId,
    Guid SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string PermissionKey,
    string ScopeType,
    string Summary,
    IReadOnlyList<AccessExplanationStepResponse> Steps,
    AccessExplanationPathResponse? MatchedPath);

public sealed record AccessExplanationStepResponse(
    int Order,
    string Code,
    string Status,
    string Message,
    IReadOnlyDictionary<string, string> Details);

public sealed record AccessExplanationPathResponse(
    string Type,
    Guid? AssignmentId,
    Guid? RoleId,
    string? RoleName,
    string ScopeType,
    Guid ScopeId,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc);
