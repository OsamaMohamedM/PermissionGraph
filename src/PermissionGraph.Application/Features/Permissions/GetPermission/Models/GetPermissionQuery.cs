namespace PermissionGraph.Application.Features.Permissions;

public sealed record GetPermissionQuery(Guid OrganizationId, Guid PermissionId);
