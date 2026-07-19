namespace PermissionGraph.Contracts.OrganizationMembers;

public sealed record AddOrganizationMemberRequest(string Email);

public sealed record OrganizationMemberResponse(
    Guid MembershipId,
    Guid OrganizationId,
    Guid UserId,
    string? Email,
    string? DisplayName,
    string Status,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    DateTimeOffset? RemovedAtUtc);

public sealed record OrganizationMemberListResponse(
    IReadOnlyList<OrganizationMemberResponse> Items,
    string? NextCursor,
    int PageSize);
