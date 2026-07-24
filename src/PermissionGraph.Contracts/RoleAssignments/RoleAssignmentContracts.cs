namespace PermissionGraph.Contracts.RoleAssignments;

public sealed record AssignRoleRequest(
    Guid UserId,
    Guid RoleId,
    string ScopeType,
    Guid ScopeId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string Reason);

public sealed record RevokeRoleAssignmentRequest(string Reason);

public sealed record RoleAssignmentResponse(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    Guid RoleId,
    string ScopeType,
    Guid ScopeId,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    Guid GrantedByUserId,
    string GrantReason,
    DateTimeOffset? RevokedAtUtc,
    Guid? RevokedByUserId,
    string? RevokeReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version);

public sealed record RoleAssignmentListResponse(
    IReadOnlyList<RoleAssignmentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
