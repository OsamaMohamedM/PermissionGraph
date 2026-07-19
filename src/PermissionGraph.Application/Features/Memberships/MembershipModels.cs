using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Application.Features.Memberships;

public sealed record AddOrganizationMemberCommand(Guid OrganizationId, string Email);

public sealed record GetOrganizationMemberQuery(Guid OrganizationId, Guid UserId);

public sealed record ListOrganizationMembersQuery(
    Guid OrganizationId,
    int PageSize = 20,
    string? Cursor = null,
    string? Search = null,
    string? Status = null);

public sealed record SuspendOrganizationMemberCommand(Guid OrganizationId, Guid UserId);

public sealed record ReactivateOrganizationMemberCommand(Guid OrganizationId, Guid UserId);

public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId);

public sealed record LeaveOrganizationCommand(Guid OrganizationId);

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
