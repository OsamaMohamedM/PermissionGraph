namespace PermissionGraph.Application.Features.Permissions;

public sealed record UpdateCustomPermissionCommand(
    Guid OrganizationId,
    Guid PermissionId,
    string DisplayName,
    string? Description,
    string Module,
    bool IsRequestable);
