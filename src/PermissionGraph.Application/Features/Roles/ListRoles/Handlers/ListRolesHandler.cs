namespace PermissionGraph.Application.Features.Roles.ListRoles.Handlers;

public sealed class ListRolesHandler(
    IValidator<ListRolesQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IRoleRepository roleRepository)
{
    public async Task<PageResult<RoleResult>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await roleCatalogAccess.RequireVisibleActiveOrganizationAsync(
            query.OrganizationId,
            actor.UserId,
            cancellationToken);

        var filters = new RoleListFilters(
            query.RoleType,
            query.ScopeType,
            query.IsActive,
            query.IsRequestable,
            query.Search);

        var result = await roleRepository.ListVisibleForOrganizationAsync(
            organization.Id,
            filters,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PageResult<RoleResult>(
            result.Items.Select(RoleResult.FromDomain).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
