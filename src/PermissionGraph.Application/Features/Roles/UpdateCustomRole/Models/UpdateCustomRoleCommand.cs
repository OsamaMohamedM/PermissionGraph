namespace PermissionGraph.Application.Features.Roles.UpdateCustomRole.Models;

public sealed record UpdateCustomRoleCommand(
    Guid OrganizationId,
    Guid RoleId,
    string Name,
    string? Description,
    bool IsRequestable);
