namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class AuthorizationRequestMappings
{
    public static CheckPermissionQuery ToQuery(this AuthorizationCheckRequest request, Guid organizationId)
    {
        return new CheckPermissionQuery(
            request.SubjectUserId,
            organizationId,
            request.ProjectId,
            request.PermissionKey);
    }

    public static BatchCheckPermissionsQuery ToQuery(this AuthorizationBatchCheckRequest request, Guid organizationId)
    {
        return new BatchCheckPermissionsQuery(
            request.Checks
                .Select(item => new BatchCheckPermissionItem(
                    item.CorrelationId,
                    item.SubjectUserId,
                    organizationId,
                    item.ProjectId,
                    item.PermissionKey))
                .ToArray());
    }

    public static ExplainAccessQuery ToQuery(this ExplainAccessRequest request, Guid organizationId)
    {
        return new ExplainAccessQuery(
            request.SubjectUserId,
            organizationId,
            request.ProjectId,
            request.PermissionKey,
            request.EvaluatedAtUtc);
    }

    public static AuthorizationDecisionResponse ToResponse(this AuthorizationDecision decision)
    {
        return new AuthorizationDecisionResponse(
            decision.Allowed,
            decision.ReasonCode,
            decision.EvaluatedAtUtc);
    }

    public static AuthorizationBatchCheckResponse ToResponse(this BatchAuthorizationDecisionResult result)
    {
        return new AuthorizationBatchCheckResponse(
            result.Items
                .Select(item => new AuthorizationBatchDecisionResponse(
                    item.CorrelationId,
                    item.Index,
                    item.Decision.ToResponse()))
                .ToArray());
    }

    public static ExplainAccessResponse ToResponse(this ExplainAccessResult result)
    {
        return new ExplainAccessResponse(
            result.Allowed,
            result.ReasonCode,
            result.EvaluatedAtUtc,
            result.ActorUserId,
            result.SubjectUserId,
            result.OrganizationId,
            result.ProjectId,
            result.PermissionKey,
            result.ScopeType.ToString(),
            result.Summary,
            result.Steps
                .Select(step => new AccessExplanationStepResponse(
                    step.Order,
                    step.Code,
                    step.Status,
                    step.Message,
                    step.Details))
                .ToArray(),
            result.MatchedPath is null
                ? null
                : new AccessExplanationPathResponse(
                    result.MatchedPath.Type,
                    result.MatchedPath.AssignmentId,
                    result.MatchedPath.RoleId,
                    result.MatchedPath.RoleName,
                    result.MatchedPath.ScopeType,
                    result.MatchedPath.ScopeId,
                    result.MatchedPath.StartsAtUtc,
                    result.MatchedPath.ExpiresAtUtc));
    }
}
