using FluentValidation;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Validation;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class GetPermissionHandler(
    IValidator<GetPermissionQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccess permissionCatalogAccess)
{
    public async Task<PermissionResult> HandleAsync(GetPermissionQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var permission = await permissionCatalogAccess.RequireVisiblePermissionAsync(
            query.OrganizationId,
            query.PermissionId,
            actor.UserId,
            cancellationToken);

        return PermissionResult.FromDomain(permission);
    }
}
