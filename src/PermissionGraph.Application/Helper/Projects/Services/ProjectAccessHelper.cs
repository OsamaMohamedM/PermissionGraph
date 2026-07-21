namespace PermissionGraph.Application.Helper.Projects.Services;

public sealed class ProjectAccessHelper(
    OrganizationAccessHelper organizationAccess,
    IProjectRepository projectRepository)
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

    public async Task<Project> RequireVisibleProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireVisibleActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var project = await projectRepository.GetByOrganizationAndIdAsync(organizationId, projectId, cancellationToken);
        if (project is null || !project.IsActive)
        {
            throw NotFound();
        }

        return project;
    }

    public async Task<Project> RequireOwnedProjectAsync(
        Guid organizationId,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await RequireOwnerActiveOrganizationAsync(organizationId, actorUserId, cancellationToken);

        var project = await projectRepository.GetByOrganizationAndIdAsync(organizationId, projectId, cancellationToken);
        if (project is null)
        {
            throw NotFound();
        }

        return project;
    }

    public static NotFoundApplicationException NotFound()
    {
        return new NotFoundApplicationException("project_not_found", "Project could not be found.");
    }
}