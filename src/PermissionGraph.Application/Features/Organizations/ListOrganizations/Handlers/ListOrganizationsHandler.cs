using FluentValidation;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Application.Common.Validation;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class ListOrganizationsHandler(
    IValidator<ListOrganizationsQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    IOrganizationRepository organizationRepository)
{
    public async Task<PagedResult<OrganizationResult>> HandleAsync(ListOrganizationsQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var result = await organizationRepository.ListForUserAsync(actor.UserId, query.PageSize, query.Cursor, cancellationToken);

        return new PagedResult<OrganizationResult>(
            result.Items.Select(OrganizationResult.FromDomain).ToArray(),
            result.NextCursor);
    }
}
