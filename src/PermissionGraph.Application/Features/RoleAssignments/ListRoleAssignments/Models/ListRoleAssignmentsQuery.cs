namespace PermissionGraph.Application.Features.RoleAssignments.ListRoleAssignments.Models;

public sealed record ListRoleAssignmentsQuery(
    Guid OrganizationId,
    Guid? UserId,
    Guid? RoleId,
    RoleAssignmentScopeType? ScopeType,
    Guid? ScopeId,
    RoleAssignmentStatus? Status,
    DateTimeOffset? EffectiveAtUtc,
    DateTimeOffset? ExpiringBeforeUtc,
    int Page,
    int PageSize);
