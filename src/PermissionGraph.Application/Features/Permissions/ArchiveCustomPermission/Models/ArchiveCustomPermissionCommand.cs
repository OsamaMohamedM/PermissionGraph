namespace PermissionGraph.Application.Features.Permissions;

public sealed record ArchiveCustomPermissionCommand(Guid OrganizationId, Guid PermissionId);
