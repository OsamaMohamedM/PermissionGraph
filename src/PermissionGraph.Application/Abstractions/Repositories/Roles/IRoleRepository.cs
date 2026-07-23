namespace PermissionGraph.Application.Abstractions.Repositories.Roles;

public interface IRoleRepository
{
    Task AddAsync(Role role, CancellationToken cancellationToken);

    Task<Role?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken);

    Task<PageResult<Role>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        RoleListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> ActiveNormalizedNameExistsAsync(
        Guid organizationId,
        RoleScopeType scopeType,
        string normalizedName,
        Guid? excludingRoleId,
        CancellationToken cancellationToken);
}

public sealed record RoleListFilters(
    RoleType? RoleType,
    RoleScopeType? ScopeType,
    bool? IsActive,
    bool? IsRequestable,
    string? Search);
