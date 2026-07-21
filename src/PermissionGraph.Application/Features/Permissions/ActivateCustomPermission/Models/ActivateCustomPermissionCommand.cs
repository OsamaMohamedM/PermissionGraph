namespace PermissionGraph.Application.Features.Permissions.ActivateCustomPermission.Models;

public sealed record ActivateCustomPermissionCommand(Guid OrganizationId, Guid PermissionId);