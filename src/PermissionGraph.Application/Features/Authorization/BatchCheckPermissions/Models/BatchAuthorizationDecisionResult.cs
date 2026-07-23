namespace PermissionGraph.Application.Features.Authorization.BatchCheckPermissions.Models;

public sealed record BatchAuthorizationDecisionResult(IReadOnlyList<BatchAuthorizationDecision> Items);

public sealed record BatchAuthorizationDecision(
    string CorrelationId,
    int Index,
    AuthorizationDecision Decision);
