namespace PermissionGraph.Application.Features.Projects.CreateProject.Models;

public sealed record CreateProjectCommand(Guid OrganizationId, string Name, string? Description);