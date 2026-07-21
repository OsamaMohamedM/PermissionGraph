namespace PermissionGraph.Application.Features.Projects.ListProjects.Handlers;

public sealed class ListProjectsHandler(
    IValidator<ListProjectsQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccessHelper projectAccess,
    IProjectRepository projectRepository)
{
    public async Task<PageResult<ProjectResult>> HandleAsync(ListProjectsQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await projectAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);
        var result = await projectRepository.ListPageForOrganizationAsync(organization.Id, query.Page, query.PageSize, cancellationToken);

        return new PageResult<ProjectResult>(
            result.Items.Select(ProjectResult.FromDomain).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}