using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Abstractions.Projects;

public interface IProjectAdministratorAssignmentService
{
    Task AssignCreatorAsProjectAdministratorAsync(Project project, Guid creatorUserId, CancellationToken cancellationToken);
}
