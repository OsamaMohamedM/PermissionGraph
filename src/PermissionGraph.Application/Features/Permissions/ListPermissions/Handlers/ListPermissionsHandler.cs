using FluentValidation;
using PermissionGraph.Application.Abstractions.Permissions;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Application.Common.Validation;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class ListPermissionsHandler(
    IValidator<ListPermissionsQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccess permissionCatalogAccess,
    IPermissionDefinitionRepository permissionRepository)
{
    public async Task<PageResult<PermissionResult>> HandleAsync(
        ListPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await permissionCatalogAccess.RequireVisibleActiveOrganizationAsync(
            query.OrganizationId,
            actor.UserId,
            cancellationToken);

        var filters = new PermissionDefinitionListFilters(
            query.PermissionType,
            query.Module,
            query.IsActive,
            query.IsRequestable,
            query.AllowedScopes,
            query.Search);

        var result = await permissionRepository.ListVisibleForOrganizationAsync(
            organization.Id,
            filters,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PageResult<PermissionResult>(
            result.Items.Select(PermissionResult.FromDomain).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
