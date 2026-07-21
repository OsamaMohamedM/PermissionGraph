namespace PermissionGraph.Application.Features.Projects.ArchiveProject.Models;

public sealed record ArchiveProjectCommand(Guid OrganizationId, Guid ProjectId, string Confirmation);