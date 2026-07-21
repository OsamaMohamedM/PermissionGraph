namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class PermissionRequestMappings
{
    public static CreateCustomPermissionCommand ToCommand(
        this CreateCustomPermissionRequest request,
        Guid organizationId)
    {
        return new CreateCustomPermissionCommand(
            organizationId,
            request.Key,
            request.DisplayName,
            request.Description,
            request.Module,
            ParseRequiredAllowedScopes(request.AllowedScopes),
            request.IsRequestable);
    }

    public static UpdateCustomPermissionCommand ToCommand(
        this UpdateCustomPermissionRequest request,
        Guid organizationId,
        Guid permissionId)
    {
        return new UpdateCustomPermissionCommand(
            organizationId,
            permissionId,
            request.DisplayName,
            request.Description,
            request.Module,
            request.IsRequestable);
    }

    public static ListPermissionsQuery ToQuery(this ListPermissionsRequest request, Guid organizationId)
    {
        return new ListPermissionsQuery(
            organizationId,
            ParsePermissionType(request.PermissionType),
            request.Module,
            request.IsActive,
            request.IsRequestable,
            ParseAllowedScopes(request.AllowedScope),
            request.Search,
            request.Page,
            request.PageSize);
    }

    public static PermissionResponse ToResponse(this PermissionResult result)
    {
        return new PermissionResponse(
            result.Id,
            result.OrganizationId,
            result.Key,
            result.DisplayName,
            result.Description,
            result.Module,
            result.PermissionType.ToString(),
            result.AllowedScopes.ToString(),
            result.IsRequestable,
            result.IsActive,
            result.CreatedAtUtc,
            result.UpdatedAtUtc,
            result.ArchivedAtUtc);
    }

    private static PermissionType? ParsePermissionType(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<PermissionType>(value, ignoreCase: true);
    }

    private static PermissionAllowedScopes? ParseAllowedScopes(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.Parse<PermissionAllowedScopes>(value, ignoreCase: true);
    }

    private static PermissionAllowedScopes ParseRequiredAllowedScopes(string value)
    {
        return Enum.Parse<PermissionAllowedScopes>(value, ignoreCase: true);
    }
}