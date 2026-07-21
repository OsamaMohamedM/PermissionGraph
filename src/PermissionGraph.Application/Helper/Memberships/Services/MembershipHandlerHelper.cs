namespace PermissionGraph.Application.Helper.Memberships.Services;

internal static class MembershipHandlerHelper
{
    public static async Task<OrganizationMembership> GetTargetMembershipAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccessHelper.NotFound();
    }

    public static async Task<OrganizationMembership> GetTargetMembershipIncludingRemovedAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserIncludingRemovedAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccessHelper.NotFound();
    }
}