namespace PermissionGraph.Application.Features.Roles.GetRole.Models;

public sealed record GetRoleQuery(Guid OrganizationId, Guid RoleId);
