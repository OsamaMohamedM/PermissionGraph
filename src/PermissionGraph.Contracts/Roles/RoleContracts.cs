namespace PermissionGraph.Contracts.Roles;

public sealed record CreateCustomRoleRequest(
    string Name,
    string? Description,
    string ScopeType,
    bool IsRequestable,
    IReadOnlyCollection<Guid> PermissionIds);

public sealed record UpdateCustomRoleRequest(
    string Name,
    string? Description,
    bool IsRequestable);

public sealed record CloneRoleRequest(
    string Name,
    string? Description,
    bool IsRequestable);

public sealed record ReplaceRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionIds);

public sealed record RoleResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string RoleType,
    string ScopeType,
    bool IsRequestable,
    bool IsActive,
    IReadOnlyCollection<Guid> PermissionIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    uint Version);

public sealed record RoleListResponse(
    IReadOnlyList<RoleResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
