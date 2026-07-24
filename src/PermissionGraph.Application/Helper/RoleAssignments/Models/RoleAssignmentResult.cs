namespace PermissionGraph.Application.Helper.RoleAssignments.Models;

public sealed record RoleAssignmentResult(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    Guid RoleId,
    RoleAssignmentScopeType ScopeType,
    Guid ScopeId,
    RoleAssignmentStatus Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    Guid GrantedByUserId,
    string GrantReason,
    DateTimeOffset? RevokedAtUtc,
    Guid? RevokedByUserId,
    string? RevokeReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version)
{
    public static RoleAssignmentResult FromDomain(RoleAssignment assignment)
    {
        return new RoleAssignmentResult(
            assignment.Id,
            assignment.OrganizationId,
            assignment.UserId,
            assignment.RoleId,
            assignment.ScopeType,
            assignment.ScopeId,
            assignment.Status,
            assignment.StartsAtUtc,
            assignment.ExpiresAtUtc,
            assignment.GrantedByUserId,
            assignment.GrantReason,
            assignment.RevokedAtUtc,
            assignment.RevokedByUserId,
            assignment.RevokeReason,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc,
            assignment.Version);
    }
}
