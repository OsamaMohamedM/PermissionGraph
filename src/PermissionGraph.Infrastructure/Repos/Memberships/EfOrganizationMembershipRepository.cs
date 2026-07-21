namespace PermissionGraph.Infrastructure.Repos.Memberships;

internal sealed class EfOrganizationMembershipRepository(PermissionGraphDbContext dbContext) : IOrganizationMembershipRepository
{
    public async Task AddAsync(OrganizationMembership membership, CancellationToken cancellationToken)
    {
        await dbContext.OrganizationMemberships.AddAsync(membership, cancellationToken);
    }

    public Task<OrganizationMembership?> GetByOrganizationAndUserAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.OrganizationMemberships.SingleOrDefaultAsync(
            membership =>
                membership.OrganizationId == organizationId &&
                membership.UserId == userId &&
                membership.Status != MembershipStatus.Removed,
            cancellationToken);
    }

    public Task<OrganizationMembership?> GetByOrganizationAndUserIncludingRemovedAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.OrganizationMemberships.SingleOrDefaultAsync(
            membership => membership.OrganizationId == organizationId && membership.UserId == userId,
            cancellationToken);
    }

    public async Task<PagedResult<OrganizationMemberResult>> ListMembersAsync(
        Guid organizationId,
        int pageSize,
        string? cursor,
        string? search,
        string? status,
        CancellationToken cancellationToken)
    {
        var query =
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId && membership.Status != MembershipStatus.Removed
            select new { membership, user };

        if (Enum.TryParse<MembershipStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(item => item.membership.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.user.NormalizedEmail!.Contains(normalizedSearch) ||
                item.user.DisplayName.ToUpper().Contains(normalizedSearch));
        }

        if (Guid.TryParse(cursor, out var cursorId))
        {
            query = query.Where(item => item.membership.Id != cursorId);
        }

        var items = await query
            .OrderBy(item => item.membership.JoinedAtUtc)
            .ThenBy(item => item.membership.Id)
            .Take(pageSize + 1)
            .Select(item => new OrganizationMemberResult(
                item.membership.Id,
                item.membership.OrganizationId,
                item.membership.UserId,
                item.user.Email,
                item.user.DisplayName,
                item.membership.Status,
                item.membership.AuthorizationVersion,
                item.membership.JoinedAtUtc,
                item.membership.SuspendedAtUtc,
                item.membership.RemovedAtUtc,
                item.membership.Version))
            .ToListAsync(cancellationToken);

        var nextCursor = items.Count > pageSize ? items[^1].MembershipId.ToString() : null;
        return new PagedResult<OrganizationMemberResult>(items.Take(pageSize).ToArray(), nextCursor);
    }

    public Task<OrganizationMemberResult?> GetMemberResultAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return (
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId && membership.UserId == userId
            select new OrganizationMemberResult(
                membership.Id,
                membership.OrganizationId,
                membership.UserId,
                user.Email,
                user.DisplayName,
                membership.Status,
                membership.AuthorizationVersion,
                membership.JoinedAtUtc,
                membership.SuspendedAtUtc,
                membership.RemovedAtUtc,
                membership.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task IncrementAuthorizationVersionAsync(
        Guid organizationId,
        Guid userId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.OrganizationMemberships
            .Where(membership => membership.OrganizationId == organizationId && membership.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(membership => membership.AuthorizationVersion, membership => membership.AuthorizationVersion + 1)
                    .SetProperty(membership => membership.UpdatedAtUtc, updatedAtUtc),
                cancellationToken);
    }
}