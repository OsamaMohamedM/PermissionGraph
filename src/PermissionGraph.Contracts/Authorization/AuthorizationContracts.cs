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
