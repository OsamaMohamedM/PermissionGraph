using PermissionGraph.Application.Abstractions.Projects;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Features.Projects;

public sealed class ProjectAccess(
    OrganizationAccess organizationAccess,
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
