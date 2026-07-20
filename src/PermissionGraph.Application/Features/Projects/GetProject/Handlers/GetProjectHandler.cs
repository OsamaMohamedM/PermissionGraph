using FluentValidation;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Validation;

namespace PermissionGraph.Application.Features.Projects;

public sealed class GetProjectHandler(
    IValidator<GetProjectQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccess projectAccess)
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
