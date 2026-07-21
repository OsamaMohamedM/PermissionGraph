namespace PermissionGraph.Application.Helper.Memberships.Models;

public sealed record OrganizationMemberResult(
    Guid MembershipId,
    Guid OrganizationId,
    Guid UserId,
    string? Email,
    string? DisplayName,
    MembershipStatus Status,
    long AuthorizationVersion,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? RemovedAtUtc,
    uint Version)
{
    public static OrganizationMemberResult FromDomain(
        OrganizationMembership membership,
        string? email = null,
        string? displayName = null)
    {
        return new OrganizationMemberResult(
            membership.Id,
            membership.OrganizationId,
            membership.UserId,
            email,
            displayName,
            membership.Status,
            membership.AuthorizationVersion,
            membership.JoinedAtUtc,
            membership.SuspendedAtUtc,
            membership.RemovedAtUtc,
            membership.Version);
    }
}