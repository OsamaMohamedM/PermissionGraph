using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

public sealed record CreateOrganizationCommand(string Name, string? Description);

public sealed record GetOrganizationQuery(Guid OrganizationId);

public sealed record ListOrganizationsQuery(int PageSize = 20, string? Cursor = null);

public sealed record UpdateOrganizationCommand(Guid OrganizationId, string Name, string? Description);

public sealed record ArchiveOrganizationCommand(Guid OrganizationId, string Confirmation);

public sealed record TransferOwnershipCommand(Guid OrganizationId, Guid NewOwnerUserId, string CurrentPassword);

public sealed record OrganizationResult(
    Guid Id,
    string Name,
    string NormalizedName,
    string? Description,
    Guid OwnerUserId,
    OrganizationStatus Status,
    long PolicyVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version)
{
    public static OrganizationResult FromDomain(Organization organization)
    {
        return new OrganizationResult(
            organization.Id,
            organization.Name,
            organization.NormalizedName,
            organization.Description,
            organization.OwnerUserId,
            organization.Status,
            organization.PolicyVersion,
            organization.CreatedAtUtc,
            organization.UpdatedAtUtc,
            organization.Version);
    }
}
