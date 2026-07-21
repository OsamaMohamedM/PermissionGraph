namespace PermissionGraph.Application.Helper.Permissions.Services;

public sealed class PermissionCatalogAccessHelper(
    OrganizationAccessHelper organizationAccess,
    IPermissionDefinitionRepository permissionRepository)
{
    public async Task<Organization> RequireVisibleActiveOrganizationAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return await organizationAccess.RequireVisibleActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);
    }

    public async Task<Organization> RequireOwnerActiveOrganizationAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return await organizationAccess.RequireOwnerActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);
    }

    public async Task<PermissionDefinition> RequireVisiblePermissionAsync(
        Guid organizationId,
        Guid permissionId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireVisibleActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var permission = await permissionRepository.GetVisibleByOrganizationAndIdAsync(
            organizationId,
            permissionId,
            cancellationToken);

        return permission ?? throw NotFound();
    }

    public async Task<PermissionDefinition> RequireOwnedCustomPermissionAsync(
        Guid organizationId,
        Guid permissionId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireOwnerActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var permission = await permissionRepository.GetOrganizationCustomByIdAsync(
            organizationId,
            permissionId,
            cancellationToken);

        return permission ?? throw NotFound();
    }

    public static NotFoundApplicationException NotFound()
    {
        return new NotFoundApplicationException("permission_not_found", "Permission could not be found.");
    }
}
