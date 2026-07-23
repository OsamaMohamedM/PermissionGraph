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
}
