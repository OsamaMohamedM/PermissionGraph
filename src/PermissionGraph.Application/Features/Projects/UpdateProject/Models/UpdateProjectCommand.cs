namespace PermissionGraph.Application.Features.Projects;

public sealed record UpdateProjectCommand(Guid OrganizationId, Guid ProjectId, string Name, string? Description);
