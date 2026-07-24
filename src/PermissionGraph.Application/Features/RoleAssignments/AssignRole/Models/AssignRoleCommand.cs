namespace PermissionGraph.Application.Features.RoleAssignments.AssignRole.Models;

public sealed record AssignRoleCommand(
    Guid OrganizationId,
    Guid UserId,
    Guid RoleId,
    RoleAssignmentScopeType ScopeType,
    Guid ScopeId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string Reason);
