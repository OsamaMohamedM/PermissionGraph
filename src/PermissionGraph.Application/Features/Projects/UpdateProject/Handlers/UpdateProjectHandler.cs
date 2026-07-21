namespace PermissionGraph.Application.Features.Projects.UpdateProject.Handlers;

public sealed class UpdateProjectHandler(
    IValidator<UpdateProjectCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccessHelper projectAccess,
    IProjectRepository projectRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<ProjectResult> HandleAsync(UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var project = await projectAccess.RequireOwnedProjectAsync(command.OrganizationId, command.ProjectId, actor.UserId, cancellationToken);
        var normalizedName = CreateProjectHandler.NormalizeName(command.Name);

        if (await projectRepository.ActiveNormalizedNameExistsAsync(project.OrganizationId, normalizedName, project.Id, cancellationToken))
        {
            throw CreateProjectHandler.DuplicateName();
        }

        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            project.UpdateDetails(command.Name, normalizedName, command.Description, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(project.OrganizationId, actor.UserId, "project.updated", "Project", project.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return ProjectResult.FromDomain(project);
    }
}