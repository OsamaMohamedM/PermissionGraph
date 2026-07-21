namespace PermissionGraph.Application.Features.Permissions.CreateCustomPermission.Models;

public sealed record CreateCustomPermissionCommand(
    Guid OrganizationId,
    string Key,
    string DisplayName,
    string? Description,
    string Module,
    PermissionAllowedScopes AllowedScopes,
    bool IsRequestable);