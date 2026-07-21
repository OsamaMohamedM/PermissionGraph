namespace PermissionGraph.Application.Features.Memberships.ListOrganizationMembers.Handlers;

public sealed class ListOrganizationMembersHandler(
    IValidator<ListOrganizationMembersQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<PagedResult<OrganizationMemberResult>> HandleAsync(ListOrganizationMembersQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        return await membershipRepository.ListMembersAsync(
            query.OrganizationId,
            query.PageSize,
            query.Cursor,
            query.Search,
            query.Status,
            cancellationToken);
    }
}