namespace PermissionGraph.Application.Features.Permissions;

public sealed record ActivateCustomPermissionCommand(Guid OrganizationId, Guid PermissionId);
