namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class RoleAssignmentRequestMappings
{
    public static AssignRoleCommand ToCommand(this AssignRoleRequest request, Guid organizationId)
    {
        return new AssignRoleCommand(
            organizationId,
            request.UserId,
            request.RoleId,
            ParseRequiredScopeType(request.ScopeType),
            request.ScopeId,
            request.StartsAtUtc,
            request.ExpiresAtUtc,
            request.Reason);
    }

    public static RevokeRoleAssignmentCommand ToCommand(
        this RevokeRoleAssignmentRequest request,
        Guid organizationId,
        Guid assignmentId)
    {
        return new RevokeRoleAssignmentCommand(organizationId, assignmentId, request.Reason);
    }

    public static ListRoleAssignmentsQuery ToQuery(
        this ListRoleAssignmentsRequest request,
        Guid organizationId)
    {
        return new ListRoleAssignmentsQuery(
            organizationId,
            request.UserId,
            request.RoleId,
            ParseScopeType(request.ScopeType),
            request.ScopeId,
            ParseStatus(request.Status),
            request.EffectiveAtUtc,
            request.ExpiringBeforeUtc,
            request.Page,
            request.PageSize);
    }

    public static RoleAssignmentResponse ToResponse(this RoleAssignmentResult result)
    {
        return new RoleAssignmentResponse(
            result.Id,
            result.OrganizationId,
            result.UserId,
            result.RoleId,
            result.ScopeType.ToString(),
            result.ScopeId,
            result.Status.ToString(),
            result.StartsAtUtc,
            result.ExpiresAtUtc,
            result.GrantedByUserId,
            result.GrantReason,
            result.RevokedAtUtc,
            result.RevokedByUserId,
            result.RevokeReason,
            result.CreatedAtUtc,
            result.UpdatedAtUtc,
            result.Version);
    }

    private static RoleAssignmentScopeType? ParseScopeType(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<RoleAssignmentScopeType>(value, ignoreCase: true);
    }

    private static RoleAssignmentScopeType ParseRequiredScopeType(string value)
    {
        return Enum.Parse<RoleAssignmentScopeType>(value, ignoreCase: true);
    }

    private static RoleAssignmentStatus? ParseStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<RoleAssignmentStatus>(value, ignoreCase: true);
    }
}
