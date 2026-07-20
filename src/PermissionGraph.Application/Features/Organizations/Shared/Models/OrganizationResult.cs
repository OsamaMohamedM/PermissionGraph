using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

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
