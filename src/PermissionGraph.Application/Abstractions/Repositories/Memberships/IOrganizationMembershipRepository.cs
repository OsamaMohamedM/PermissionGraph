namespace PermissionGraph.Application.Abstractions.Repositories.Memberships;

public interface IOrganizationMembershipRepository
{
    Task AddAsync(OrganizationMembership membership, CancellationToken cancellationToken);

    Task<OrganizationMembership?> GetByOrganizationAndUserAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> GetByOrganizationAndUserIncludingRemovedAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<PagedResult<OrganizationMemberResult>> ListMembersAsync(
        Guid organizationId,
        int pageSize,
        string? cursor,
        string? search,
        string? status,
        CancellationToken cancellationToken);

    Task<OrganizationMemberResult?> GetMemberResultAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task IncrementAuthorizationVersionAsync(Guid organizationId, Guid userId, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
}