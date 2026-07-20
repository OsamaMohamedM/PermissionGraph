using FluentValidation;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Validation;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class GetOrganizationHandler(
    IValidator<GetOrganizationQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess)
{
    public async Task<OrganizationResult> HandleAsync(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }
}
