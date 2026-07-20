namespace PermissionGraph.Application.Features.Projects;

public sealed record CreateProjectCommand(Guid OrganizationId, string Name, string? Description);
