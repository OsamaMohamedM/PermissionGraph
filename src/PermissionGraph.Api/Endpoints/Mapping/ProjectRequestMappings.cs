using PermissionGraph.Application.Features.Projects;
using PermissionGraph.Contracts.Projects;

namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class ProjectRequestMappings
{
    public static CreateProjectCommand ToCommand(this CreateProjectRequest request, Guid organizationId)
    {
        return new CreateProjectCommand(organizationId, request.Name, request.Description);
    }

    public static UpdateProjectCommand ToCommand(this UpdateProjectRequest request, Guid organizationId, Guid projectId)
    {
        return new UpdateProjectCommand(organizationId, projectId, request.Name, request.Description);
    }

    public static ListProjectsQuery ToQuery(this ListProjectsRequest request, Guid organizationId)
    {
        return new ListProjectsQuery(organizationId, request.Page, request.PageSize);
    }

    public static ProjectResponse ToResponse(this ProjectResult result)
    {
        return new ProjectResponse(
            result.Id,
            result.OrganizationId,
            result.Name,
            result.Description,
            result.Status.ToString(),
            result.CreatedAtUtc,
            result.UpdatedAtUtc,
            result.ArchivedAtUtc);
    }
}
