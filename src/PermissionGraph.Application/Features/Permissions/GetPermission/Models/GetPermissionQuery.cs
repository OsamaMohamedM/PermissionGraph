namespace PermissionGraph.Application.Features.Permissions.GetPermission.Models;

public sealed record GetPermissionQuery(Guid OrganizationId, Guid PermissionId);