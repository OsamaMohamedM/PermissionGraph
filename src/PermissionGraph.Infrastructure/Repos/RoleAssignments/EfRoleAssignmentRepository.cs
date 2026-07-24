namespace PermissionGraph.Infrastructure.Repos.RoleAssignments;

internal sealed class EfRoleAssignmentRepository(PermissionGraphDbContext dbContext) : IRoleAssignmentRepository
{
    public async Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken)
    {
        await dbContext.RoleAssignments.AddAsync(assignment, cancellationToken);
    }

    public Task<RoleAssignment?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return dbContext.RoleAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                assignment => assignment.OrganizationId == organizationId && assignment.Id == assignmentId,
                cancellationToken);
    }

    public Task<RoleAssignment?> GetByOrganizationAndIdForMutationAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return dbContext.RoleAssignments
            .SingleOrDefaultAsync(
                assignment => assignment.OrganizationId == organizationId && assignment.Id == assignmentId,
                cancellationToken);
    }

    public async Task<PageResult<RoleAssignment>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        RoleAssignmentListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<RoleAssignment> query = dbContext.RoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganizationId == organizationId);

        if (filters.UserId is not null)
        {
            query = query.Where(assignment => assignment.UserId == filters.UserId);
        }

        if (filters.RoleId is not null)
        {
            query = query.Where(assignment => assignment.RoleId == filters.RoleId);
        }

        if (filters.ScopeType is not null)
        {
            query = query.Where(assignment => assignment.ScopeType == filters.ScopeType);
        }

        if (filters.ScopeId is not null)
        {
            query = query.Where(assignment => assignment.ScopeId == filters.ScopeId);
        }

        if (filters.Status is not null)
        {
            query = query.Where(assignment => assignment.Status == filters.Status);
        }

        if (filters.EffectiveAtUtc is not null)
        {
            var effectiveAt = filters.EffectiveAtUtc.Value;
            query = query.Where(assignment =>
                (assignment.Status == RoleAssignmentStatus.Active || assignment.Status == RoleAssignmentStatus.Scheduled) &&
                assignment.StartsAtUtc <= effectiveAt &&
                (assignment.ExpiresAtUtc == null || effectiveAt < assignment.ExpiresAtUtc));
        }

        if (filters.ExpiringBeforeUtc is not null)
        {
            query = query.Where(assignment =>
                assignment.ExpiresAtUtc != null &&
                assignment.ExpiresAtUtc <= filters.ExpiringBeforeUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(assignment => assignment.CreatedAtUtc)
            .ThenBy(assignment => assignment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<RoleAssignment>(items, page, pageSize, totalCount);
    }

    public Task<bool> HasEffectiveAssignmentAsync(
        Guid organizationId,
        Guid userId,
        Guid roleId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return dbContext.RoleAssignments.AnyAsync(
            assignment =>
                assignment.OrganizationId == organizationId &&
                assignment.UserId == userId &&
                assignment.RoleId == roleId &&
                assignment.ScopeType == scopeType &&
                assignment.ScopeId == scopeId &&
                (assignment.Status == RoleAssignmentStatus.Active || assignment.Status == RoleAssignmentStatus.Scheduled),
            cancellationToken);
    }

    public async Task<IReadOnlyList<RoleAssignment>> ListExpiredForUpdateAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.RoleAssignments
            .Where(assignment =>
                assignment.ExpiresAtUtc != null &&
                assignment.ExpiresAtUtc <= nowUtc &&
                (assignment.Status == RoleAssignmentStatus.Active || assignment.Status == RoleAssignmentStatus.Scheduled))
            .OrderBy(assignment => assignment.ExpiresAtUtc)
            .ThenBy(assignment => assignment.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
