using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Features.Projects;

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
