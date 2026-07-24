namespace PermissionGraph.Application.Abstractions.Repositories.RoleAssignments;

public interface IRoleAssignmentRepository
{
    Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken);

    Task<RoleAssignment?> GetVisibleByOrganizationAndIdAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<RoleAssignment?> GetByOrganizationAndIdForMutationAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<PageResult<RoleAssignment>> ListVisibleForOrganizationAsync(
        Guid organizationId,
        RoleAssignmentListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> HasEffectiveAssignmentAsync(
        Guid organizationId,
        Guid userId,
        Guid roleId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleAssignment>> ListExpiredForUpdateAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record RoleAssignmentListFilters(
    Guid? UserId,
    Guid? RoleId,
    RoleAssignmentScopeType? ScopeType,
    Guid? ScopeId,
    RoleAssignmentStatus? Status,
    DateTimeOffset? EffectiveAtUtc,
    DateTimeOffset? ExpiringBeforeUtc);
