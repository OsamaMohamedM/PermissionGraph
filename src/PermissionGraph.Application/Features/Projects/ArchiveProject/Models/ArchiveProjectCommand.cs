namespace PermissionGraph.Application.Features.Projects;

public sealed record ArchiveProjectCommand(Guid OrganizationId, Guid ProjectId, string Confirmation);
