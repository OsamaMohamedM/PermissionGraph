using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class OrganizationAccess(
    IOrganizationRepository organizationRepository,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<Organization> RequireVisibleActiveOrganizationAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null || !organization.IsActive)
        {
            throw NotFound();
        }

        if (organization.OwnerUserId == actorUserId)
        {
            return organization;
        }

        var membership = await membershipRepository.GetByOrganizationAndUserAsync(organizationId, actorUserId, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            throw NotFound();
        }

        return organization;
    }

    public async Task<Organization> RequireOwnerActiveOrganizationAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null || !organization.IsActive)
        {
            throw NotFound();
        }

        if (organization.OwnerUserId != actorUserId)
        {
            var membership = await membershipRepository.GetByOrganizationAndUserAsync(organizationId, actorUserId, cancellationToken);
            if (membership is null)
            {
                throw NotFound();
            }

            throw new ForbiddenApplicationException("owner_required", "Only the organization owner may perform this operation.");
        }

        return organization;
    }

    public static void EnsureMutableMembershipTarget(Organization organization, OrganizationMembership membership)
    {
        if (membership.OrganizationId != organization.Id)
        {
            throw NotFound();
        }
    }

    public static NotFoundApplicationException NotFound()
    {
        return new NotFoundApplicationException("organization_not_found", "Organization could not be found.");
    }
}
