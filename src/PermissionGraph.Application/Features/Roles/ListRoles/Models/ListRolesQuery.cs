namespace PermissionGraph.Application.Features.Roles.ListRoles.Models;

public sealed record ListRolesQuery(
    Guid OrganizationId,
    RoleType? RoleType = null,
    RoleScopeType? ScopeType = null,
    bool? IsActive = null,
    bool? IsRequestable = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
