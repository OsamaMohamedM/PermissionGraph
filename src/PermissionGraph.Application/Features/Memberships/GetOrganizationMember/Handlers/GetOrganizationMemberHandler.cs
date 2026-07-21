namespace PermissionGraph.Application.Features.Memberships.GetOrganizationMember.Handlers;

public sealed class GetOrganizationMemberHandler(
    IValidator<GetOrganizationMemberQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<OrganizationMemberResult> HandleAsync(GetOrganizationMemberQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        var result = await membershipRepository.GetMemberResultAsync(query.OrganizationId, query.UserId, cancellationToken);
        if (result is null || result.Status == MembershipStatus.Removed)
        {
            throw OrganizationAccessHelper.NotFound();
        }

        return result;
    }
}