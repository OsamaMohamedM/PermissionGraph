namespace PermissionGraph.Application.Abstractions.Repositories.Organizations;

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken);

    Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PagedResult<Organization>> ListForUserAsync(Guid userId, int pageSize, string? cursor, CancellationToken cancellationToken);
}