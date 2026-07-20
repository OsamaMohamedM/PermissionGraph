namespace PermissionGraph.Application.Features.Projects;

public sealed record ListProjectsQuery(Guid OrganizationId, int Page = 1, int PageSize = 20);
