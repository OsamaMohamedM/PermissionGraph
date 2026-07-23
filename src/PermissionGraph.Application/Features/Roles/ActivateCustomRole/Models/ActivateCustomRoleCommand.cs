namespace PermissionGraph.Application.Features.Roles.ActivateCustomRole.Models;

public sealed record ActivateCustomRoleCommand(Guid OrganizationId, Guid RoleId);
