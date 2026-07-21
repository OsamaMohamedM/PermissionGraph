namespace PermissionGraph.Application.Features.Permissions.ArchiveCustomPermission.Models;

public sealed record ArchiveCustomPermissionCommand(Guid OrganizationId, Guid PermissionId);