namespace PermissionGraph.Application.Features.Projects.GetProject.Models;

public sealed record GetProjectQuery(Guid OrganizationId, Guid ProjectId);