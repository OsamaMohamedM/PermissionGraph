namespace PermissionGraph.Infrastructure.Repos.Projects;

internal sealed class EfProjectRepository(PermissionGraphDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await dbContext.Projects.AddAsync(project, cancellationToken);
    }

    public Task<Project?> GetByOrganizationAndIdAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Projects.SingleOrDefaultAsync(
            project => project.OrganizationId == organizationId && project.Id == projectId,
            cancellationToken);
    }

    public async Task<PageResult<Project>> ListPageForOrganizationAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Project> query = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId && project.Status == ProjectStatus.Active);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(project => project.CreatedAtUtc)
            .ThenBy(project => project.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Project>(items, page, pageSize, totalCount);
    }

    public Task<bool> ActiveNormalizedNameExistsAsync(
        Guid organizationId,
        string normalizedName,
        Guid? excludingProjectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Projects.AnyAsync(
            project =>
                project.OrganizationId == organizationId &&
                project.NormalizedName == normalizedName &&
                project.Status == ProjectStatus.Active &&
                project.Id != excludingProjectId,
            cancellationToken);
    }
}