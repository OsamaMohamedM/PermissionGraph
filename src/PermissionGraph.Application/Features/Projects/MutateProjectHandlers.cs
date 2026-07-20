using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Projects;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Domain.Common;

namespace PermissionGraph.Application.Features.Projects;

public sealed class UpdateProjectHandler(
    IValidator<UpdateProjectCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccess projectAccess,
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

public sealed class ArchiveProjectHandler(
    IValidator<ArchiveProjectCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccess projectAccess,
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
