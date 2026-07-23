namespace PermissionGraph.Infrastructure.Repos.Roles;

internal sealed class EfRoleRepository(PermissionGraphDbContext dbContext) : IRoleRepository
{
    public async Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        await dbContext.Roles.AddAsync(role, cancellationToken);
    }

    public Task<Role?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return dbContext.Roles
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(
                role => role.OrganizationId == organizationId && role.Id == roleId,
                cancellationToken);
    }

    public async Task<PageResult<Role>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        RoleListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Role> query = dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .Where(role => role.OrganizationId == organizationId);

        if (filters.RoleType is not null)
        {
            query = query.Where(role => role.RoleType == filters.RoleType);
        }

        if (filters.ScopeType is not null)
        {
            query = query.Where(role => role.ScopeType == filters.ScopeType);
        }

        if (filters.IsActive is not null)
        {
            query = query.Where(role => role.IsActive == filters.IsActive);
        }

        if (filters.IsRequestable is not null)
        {
            query = query.Where(role => role.IsRequestable == filters.IsRequestable);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim().ToUpperInvariant();
            query = query.Where(role => role.NormalizedName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(role => role.ScopeType)
            .ThenBy(role => role.Name)
            .ThenBy(role => role.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Role>(items, page, pageSize, totalCount);
    }

    public Task<bool> ActiveNormalizedNameExistsAsync(
        Guid organizationId,
        RoleScopeType scopeType,
        string normalizedName,
        Guid? excludingRoleId,
        CancellationToken cancellationToken)
    {
        return dbContext.Roles.AnyAsync(
            role =>
                role.OrganizationId == organizationId &&
                role.ScopeType == scopeType &&
                role.IsActive &&
                role.NormalizedName == normalizedName &&
                role.Id != excludingRoleId,
            cancellationToken);
    }
}
