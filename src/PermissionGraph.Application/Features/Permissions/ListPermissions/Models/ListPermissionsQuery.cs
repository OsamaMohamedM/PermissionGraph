namespace PermissionGraph.Application.Features.Permissions.ListPermissions.Models;

public sealed record ListPermissionsQuery(
    Guid OrganizationId,
    PermissionType? PermissionType = null,
    string? Module = null,
    bool? IsActive = null,
    bool? IsRequestable = null,
    PermissionAllowedScopes? AllowedScopes = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);