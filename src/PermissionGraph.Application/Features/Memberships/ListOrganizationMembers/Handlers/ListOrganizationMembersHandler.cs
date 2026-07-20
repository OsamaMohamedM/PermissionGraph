using FluentValidation;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Application.Features.Organizations;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class ListOrganizationMembersHandler(
    IValidator<ListOrganizationMembersQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
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
