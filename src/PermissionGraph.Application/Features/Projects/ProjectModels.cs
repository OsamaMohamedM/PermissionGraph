using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Features.Projects;

public sealed record CreateProjectCommand(Guid OrganizationId, string Name, string? Description);

public sealed record ListProjectsQuery(Guid OrganizationId, int Page = 1, int PageSize = 20);

public sealed record GetProjectQuery(Guid OrganizationId, Guid ProjectId);

public sealed record UpdateProjectCommand(Guid OrganizationId, Guid ProjectId, string Name, string? Description);

public sealed record ArchiveProjectCommand(Guid OrganizationId, Guid ProjectId, string Confirmation);

public sealed record ProjectResult(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string NormalizedName,
    string? Description,
    ProjectStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    uint Version)
{
    public static ProjectResult FromDomain(Project project)
    {
        return new ProjectResult(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.NormalizedName,
            project.Description,
            project.Status,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            project.ArchivedAtUtc,
            project.Version);
    }
}
