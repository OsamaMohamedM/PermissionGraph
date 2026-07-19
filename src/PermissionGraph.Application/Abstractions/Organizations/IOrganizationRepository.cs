using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Abstractions.Organizations;

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken);

    Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PagedResult<Organization>> ListForUserAsync(Guid userId, int pageSize, string? cursor, CancellationToken cancellationToken);
}
