namespace PermissionGraph.Application.Features.Projects.ArchiveProject.Handlers;

public sealed class ArchiveProjectHandler(
    IValidator<ArchiveProjectCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccessHelper projectAccess,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task HandleAsync(ArchiveProjectCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var project = await projectAccess.RequireOwnedProjectAsync(command.OrganizationId, command.ProjectId, actor.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            project.Archive(now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(project.OrganizationId, actor.UserId, "project.archived", "Project", project.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }
}