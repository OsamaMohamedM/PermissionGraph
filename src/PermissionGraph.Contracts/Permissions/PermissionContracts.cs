namespace PermissionGraph.Contracts.Permissions;

public sealed record CreateCustomPermissionRequest(
    string Key,
    string DisplayName,
    string? Description,
    string Module,
    string AllowedScopes,
    bool IsRequestable);

public sealed record UpdateCustomPermissionRequest(
    string DisplayName,
    string? Description,
    string Module,
    bool IsRequestable);

public sealed record PermissionResponse(
    Guid Id,
    Guid? OrganizationId,
    string Key,
    string DisplayName,
    string? Description,
    string Module,
    string PermissionType,
    string AllowedScopes,
    bool IsRequestable,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record PermissionListResponse(
    IReadOnlyList<PermissionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
