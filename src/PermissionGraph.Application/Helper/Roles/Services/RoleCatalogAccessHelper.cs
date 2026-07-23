namespace PermissionGraph.Application.Helper.Roles.Services;

public sealed class RoleCatalogAccessHelper(
    OrganizationAccessHelper organizationAccess,
    IRoleRepository roleRepository)
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

    public async Task<Role> RequireVisibleRoleAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireVisibleActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var role = await roleRepository.GetVisibleByOrganizationAndIdAsync(
            organizationId,
            roleId,
            cancellationToken);

        return role ?? throw NotFound();
    }

    public async Task<Role> RequireOwnerVisibleRoleAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireOwnerActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var role = await roleRepository.GetVisibleByOrganizationAndIdAsync(
            organizationId,
            roleId,
            cancellationToken);

        return role ?? throw NotFound();
    }

    public static NotFoundApplicationException NotFound()
    {
        return new NotFoundApplicationException("role_not_found", "Role could not be found.");
    }
}
