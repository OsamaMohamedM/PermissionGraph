namespace PermissionGraph.Application.Features.Organizations.GetOrganization.Handlers;

public sealed class GetOrganizationHandler(
    IValidator<GetOrganizationQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess)
{
    public async Task<OrganizationResult> HandleAsync(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }
}