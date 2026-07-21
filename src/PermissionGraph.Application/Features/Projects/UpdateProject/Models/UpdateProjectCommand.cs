namespace PermissionGraph.Application.Features.Projects.UpdateProject.Models;

public sealed record UpdateProjectCommand(Guid OrganizationId, Guid ProjectId, string Name, string? Description);