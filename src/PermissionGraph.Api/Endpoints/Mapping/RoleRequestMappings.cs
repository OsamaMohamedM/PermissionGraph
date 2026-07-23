namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class RoleRequestMappings
{
    public static CreateCustomRoleCommand ToCommand(this CreateCustomRoleRequest request, Guid organizationId)
    {
        return new CreateCustomRoleCommand(
            organizationId,
            request.Name,
            request.Description,
            ParseRequiredScopeType(request.ScopeType),
            request.IsRequestable,
            request.PermissionIds);
    }

    public static UpdateCustomRoleCommand ToCommand(
        this UpdateCustomRoleRequest request,
        Guid organizationId,
        Guid roleId)
    {
        return new UpdateCustomRoleCommand(
            organizationId,
            roleId,
            request.Name,
            request.Description,
            request.IsRequestable);
    }

    public static CloneRoleCommand ToCommand(this CloneRoleRequest request, Guid organizationId, Guid roleId)
    {
        return new CloneRoleCommand(
            organizationId,
            roleId,
            request.Name,
            request.Description,
            request.IsRequestable);
    }

    public static ReplaceRolePermissionsCommand ToCommand(
        this ReplaceRolePermissionsRequest request,
        Guid organizationId,
        Guid roleId)
    {
        return new ReplaceRolePermissionsCommand(organizationId, roleId, request.PermissionIds);
    }

    public static ListRolesQuery ToQuery(this ListRolesRequest request, Guid organizationId)
    {
        return new ListRolesQuery(
            organizationId,
            ParseRoleType(request.RoleType),
            ParseScopeType(request.ScopeType),
            request.IsActive,
            request.IsRequestable,
            request.Search,
            request.Page,
            request.PageSize);
    }

    public static RoleResponse ToResponse(this RoleResult result)
    {
        return new RoleResponse(
            result.Id,
            result.OrganizationId,
            result.Name,
            result.Description,
            result.RoleType.ToString(),
            result.ScopeType.ToString(),
            result.IsRequestable,
            result.IsActive,
            result.PermissionIds,
            result.CreatedAtUtc,
            result.UpdatedAtUtc,
            result.ArchivedAtUtc,
            result.Version);
    }

    private static RoleType? ParseRoleType(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<RoleType>(value, ignoreCase: true);
    }

    private static RoleScopeType? ParseScopeType(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<RoleScopeType>(value, ignoreCase: true);
    }

    private static RoleScopeType ParseRequiredScopeType(string value)
    {
        return Enum.Parse<RoleScopeType>(value, ignoreCase: true);
    }
}
