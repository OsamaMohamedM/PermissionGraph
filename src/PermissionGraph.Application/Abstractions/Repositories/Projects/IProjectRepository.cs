namespace PermissionGraph.Application.Abstractions.Repositories.Projects;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);

    Task<Project?> GetByOrganizationAndIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    Task<PageResult<Project>> ListPageForOrganizationAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> ActiveNormalizedNameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludingProjectId,
        CancellationToken cancellationToken);
}