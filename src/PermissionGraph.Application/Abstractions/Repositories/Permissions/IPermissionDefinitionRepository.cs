namespace PermissionGraph.Application.Abstractions.Repositories.Permissions;

public interface IPermissionDefinitionRepository
{
    Task AddAsync(PermissionDefinition permission, CancellationToken cancellationToken);

    Task<PermissionDefinition?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid permissionId,
        CancellationToken cancellationToken);

    Task<PermissionDefinition?> GetOrganizationCustomByIdAsync(
        Guid organizationId,
        Guid permissionId,
        CancellationToken cancellationToken);

    Task<PageResult<PermissionDefinition>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        PermissionDefinitionListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> CustomNormalizedKeyExistsAsync(
        Guid organizationId,
        string normalizedKey,
        Guid? excludingPermissionId,
        CancellationToken cancellationToken);
}

public sealed record PermissionDefinitionListFilters(
    PermissionType? PermissionType,
    string? Module,
    bool? IsActive,
    bool? IsRequestable,
    PermissionAllowedScopes? AllowedScopes,
    string? Search);