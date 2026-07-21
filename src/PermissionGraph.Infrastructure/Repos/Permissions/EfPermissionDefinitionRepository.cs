namespace PermissionGraph.Infrastructure.Repos.Permissions;

internal sealed class EfPermissionDefinitionRepository(PermissionGraphDbContext dbContext) : IPermissionDefinitionRepository
{
    public async Task AddAsync(PermissionDefinition permission, CancellationToken cancellationToken)
    {
        await dbContext.PermissionDefinitions.AddAsync(permission, cancellationToken);
    }

    public Task<PermissionDefinition?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        return dbContext.PermissionDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                permission =>
                    permission.Id == permissionId &&
                    (permission.PermissionType == PermissionType.Platform ||
                     permission.OrganizationId == organizationId),
                cancellationToken);
    }

    public Task<PermissionDefinition?> GetOrganizationCustomByIdAsync(
        Guid organizationId,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        return dbContext.PermissionDefinitions.SingleOrDefaultAsync(
            permission =>
                permission.Id == permissionId &&
                permission.PermissionType == PermissionType.Custom &&
                permission.OrganizationId == organizationId,
            cancellationToken);
    }

    public async Task<PageResult<PermissionDefinition>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        PermissionDefinitionListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<PermissionDefinition> query = dbContext.PermissionDefinitions
            .AsNoTracking()
            .Where(permission =>
                permission.PermissionType == PermissionType.Platform ||
                permission.OrganizationId == organizationId);

        if (filters.PermissionType is not null)
        {
            query = query.Where(permission => permission.PermissionType == filters.PermissionType);
        }

        if (!string.IsNullOrWhiteSpace(filters.Module))
        {
            query = query.Where(permission => permission.Module == filters.Module.Trim());
        }

        if (filters.IsActive is not null)
        {
            query = query.Where(permission => permission.IsActive == filters.IsActive);
        }

        if (filters.IsRequestable is not null)
        {
            query = query.Where(permission => permission.IsRequestable == filters.IsRequestable);
        }

        if (filters.AllowedScopes is not null)
        {
            query = query.Where(permission => permission.AllowedScopes == filters.AllowedScopes);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim().ToLowerInvariant();
            query = query.Where(permission =>
                permission.NormalizedKey.Contains(search) ||
                permission.DisplayName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Key)
            .ThenBy(permission => permission.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<PermissionDefinition>(items, page, pageSize, totalCount);
    }

    public Task<bool> CustomNormalizedKeyExistsAsync(
        Guid organizationId,
        string normalizedKey,
        Guid? excludingPermissionId,
        CancellationToken cancellationToken)
    {
        return dbContext.PermissionDefinitions.AnyAsync(
            permission =>
                permission.PermissionType == PermissionType.Custom &&
                permission.OrganizationId == organizationId &&
                permission.NormalizedKey == normalizedKey &&
                permission.Id != excludingPermissionId,
            cancellationToken);
    }
}