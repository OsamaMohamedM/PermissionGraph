namespace PermissionGraph.Application.Features.Projects.GetProject.Handlers;

public sealed class GetProjectHandler(
    IValidator<GetProjectQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccessHelper projectAccess)
{
    public async Task<ProjectResult> HandleAsync(GetProjectQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var project = await projectAccess.RequireVisibleProjectAsync(
            query.OrganizationId,
            query.ProjectId,
            actor.UserId,
            cancellationToken);

        return ProjectResult.FromDomain(project);
    }
}