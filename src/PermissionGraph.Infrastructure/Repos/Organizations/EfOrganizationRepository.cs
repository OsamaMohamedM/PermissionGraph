using Microsoft.EntityFrameworkCore;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Infrastructure.Data;

namespace PermissionGraph.Infrastructure.Organizations;

internal sealed class EfOrganizationRepository(PermissionGraphDbContext dbContext) : IOrganizationRepository
{
    public async Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        await dbContext.Organizations.AddAsync(organization, cancellationToken);
    }

    public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return dbContext.Organizations.SingleOrDefaultAsync(organization => organization.Id == organizationId, cancellationToken);
    }

    public async Task<PagedResult<Organization>> ListForUserAsync(Guid userId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        IQueryable<Organization> query = dbContext.Organizations
            .AsNoTracking()
            .Where(organization =>
                organization.OwnerUserId == userId ||
                dbContext.OrganizationMemberships.Any(membership =>
                    membership.OrganizationId == organization.Id &&
                    membership.UserId == userId &&
                    membership.Status != MembershipStatus.Removed));

        if (Guid.TryParse(cursor, out var cursorId))
        {
            query = query.Where(organization => organization.Id != cursorId);
        }

        var items = await query
            .OrderBy(organization => organization.CreatedAtUtc)
            .ThenBy(organization => organization.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        var nextCursor = items.Count > pageSize ? items[^1].Id.ToString() : null;

        return new PagedResult<Organization>(items.Take(pageSize).ToArray(), nextCursor);
    }
}
