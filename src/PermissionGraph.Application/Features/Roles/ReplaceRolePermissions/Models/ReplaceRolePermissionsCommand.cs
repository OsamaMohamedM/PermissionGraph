namespace PermissionGraph.Application.Features.Roles.ReplaceRolePermissions.Models;

public sealed record ReplaceRolePermissionsCommand(
    Guid OrganizationId,
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds);
