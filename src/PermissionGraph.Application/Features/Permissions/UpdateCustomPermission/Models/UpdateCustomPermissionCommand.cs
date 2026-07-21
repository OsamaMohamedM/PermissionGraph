namespace PermissionGraph.Application.Features.Permissions.UpdateCustomPermission.Models;

public sealed record UpdateCustomPermissionCommand(
    Guid OrganizationId,
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string Module,
    bool IsRequestable);