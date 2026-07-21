namespace PermissionGraph.Application.Abstractions.Services.Projects;

public interface IProjectAdministratorAssignmentService
{
    Task AssignCreatorAsProjectAdministratorAsync(Project project, Guid creatorUserId, CancellationToken cancellationToken);
}