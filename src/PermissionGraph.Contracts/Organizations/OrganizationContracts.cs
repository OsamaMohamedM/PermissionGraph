namespace PermissionGraph.Contracts.Organizations;

public sealed record CreateOrganizationRequest(string Name, string? Description);

public sealed record UpdateOrganizationRequest(string Name, string? Description);

public sealed record ArchiveOrganizationRequest(string Confirmation);

public sealed record TransferOwnershipRequest(Guid NewOwnerUserId, string CurrentPassword);

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OrganizationListResponse(
    IReadOnlyList<OrganizationResponse> Items,
    string? NextCursor,
    int PageSize);