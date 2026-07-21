namespace PermissionGraph.Infrastructure.Services.Projects;

internal sealed class EfProjectAdministratorAssignmentService(
    PermissionGraphDbContext dbContext,
    IAuditWriter auditWriter,
    IGuidProvider guidProvider,
    IClock clock) : IProjectAdministratorAssignmentService
{
    private const string ProjectAdministratorRoleName = "PROJECT ADMINISTRATOR";

    public async Task AssignCreatorAsProjectAdministratorAsync(
        Project project,
        Guid creatorUserId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            item =>
                item.OrganizationId == project.OrganizationId &&
                item.NormalizedName == ProjectAdministratorRoleName &&
                item.ScopeType == "Project" &&
                item.RoleType == "System" &&
                item.IsActive,
            cancellationToken);

        if (role is null)
        {
            throw new InvalidOperationException("Required Project Administrator role is missing.");
        }

        var exists = await dbContext.ProjectAdministratorAssignments.AnyAsync(
            item =>
                item.OrganizationId == project.OrganizationId &&
                item.ProjectId == project.Id &&
                item.UserId == creatorUserId &&
                item.RoleId == role.Id,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var now = clock.UtcNow;
        dbContext.ProjectAdministratorAssignments.Add(new ProjectAdministratorAssignmentRecord
        {
            Id = guidProvider.NewGuid(),
            OrganizationId = project.OrganizationId,
            ProjectId = project.Id,
            UserId = creatorUserId,
            RoleId = role.Id,
            CreatedAtUtc = now,
            CreatedByUserId = creatorUserId
        });

        await auditWriter.WriteAsync(
            new AuditRecord(project.OrganizationId, creatorUserId, "project.administrator_assigned", "Project", project.Id, "Succeeded", now),
            cancellationToken);
    }
}