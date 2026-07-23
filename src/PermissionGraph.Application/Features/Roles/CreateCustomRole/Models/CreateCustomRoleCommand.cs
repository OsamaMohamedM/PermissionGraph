namespace PermissionGraph.Application.Features.Roles.CreateCustomRole.Models;

public sealed record CreateCustomRoleCommand(
    Guid OrganizationId,
    string Name,
    string? Description,
    RoleScopeType ScopeType,
    bool IsRequestable,
    IReadOnlyCollection<Guid> PermissionIds);
