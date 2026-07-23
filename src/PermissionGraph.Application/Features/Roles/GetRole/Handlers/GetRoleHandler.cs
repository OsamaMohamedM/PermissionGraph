namespace PermissionGraph.Application.Features.Roles.GetRole.Handlers;

public sealed class GetRoleHandler(
    IValidator<GetRoleQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess)
{
    public async Task<RoleResult> HandleAsync(GetRoleQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var role = await roleCatalogAccess.RequireVisibleRoleAsync(
            query.OrganizationId,
            query.RoleId,
            actor.UserId,
            cancellationToken);

        return RoleResult.FromDomain(role);
    }
}
