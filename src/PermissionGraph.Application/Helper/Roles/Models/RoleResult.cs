namespace PermissionGraph.Application.Helper.Roles.Models;

public sealed record RoleResult(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string NormalizedName,
    string? Description,
    RoleScopeType ScopeType,
    RoleType RoleType,
    bool IsRequestable,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyCollection<Guid> PermissionIds,
    uint Version)
{
    public static RoleResult FromDomain(Role role)
    {
        return new RoleResult(
            role.Id,
            role.OrganizationId,
            role.Name,
            role.NormalizedName,
            role.Description,
            role.ScopeType,
            role.RoleType,
            role.IsRequestable,
            role.IsActive,
            role.CreatedAtUtc,
            role.UpdatedAtUtc,
            role.ArchivedAtUtc,
            role.Permissions.Select(permission => permission.PermissionId).ToArray(),
            role.Version);
    }
}
