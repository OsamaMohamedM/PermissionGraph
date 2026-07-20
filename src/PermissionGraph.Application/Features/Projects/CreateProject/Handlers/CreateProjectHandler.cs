using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Identifiers;
using PermissionGraph.Application.Abstractions.Projects;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Features.Projects;

public sealed class CreateProjectHandler(
    IValidator<CreateProjectCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    ProjectAccess projectAccess,
    IProjectRepository projectRepository,
    IProjectAdministratorAssignmentService projectAdministratorAssignmentService,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<ProjectResult> HandleAsync(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await projectAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var normalizedName = NormalizeName(command.Name);

        if (await projectRepository.ActiveNormalizedNameExistsAsync(organization.Id, normalizedName, excludingProjectId: null, cancellationToken))
        {
            throw DuplicateName();
        }

        var now = clock.UtcNow;
        var project = Project.Create(
            guidProvider.NewGuid(),
            organization.Id,
            command.Name,
            normalizedName,
            command.Description,
            now);

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await projectRepository.AddAsync(project, cancellationToken);
        await projectAdministratorAssignmentService.AssignCreatorAsProjectAdministratorAsync(project, actor.UserId, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "project.created", "Project", project.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return ProjectResult.FromDomain(project);
    }

    internal static string NormalizeName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }

    internal static ConflictApplicationException DuplicateName()
    {
        return new ConflictApplicationException("project_name_already_exists", "An active project with this name already exists.");
    }
}
