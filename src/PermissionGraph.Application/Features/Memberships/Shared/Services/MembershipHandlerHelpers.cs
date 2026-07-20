using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Application.Features.Memberships;

internal static class MembershipHandlerHelpers
{
    public static async Task<OrganizationMembership> GetTargetMembershipAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccess.NotFound();
    }

    public static async Task<OrganizationMembership> GetTargetMembershipIncludingRemovedAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserIncludingRemovedAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccess.NotFound();
    }
}
