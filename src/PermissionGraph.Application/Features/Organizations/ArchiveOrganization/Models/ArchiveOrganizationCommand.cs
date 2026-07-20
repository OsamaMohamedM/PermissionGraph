namespace PermissionGraph.Application.Features.Organizations;

public sealed record ArchiveOrganizationCommand(Guid OrganizationId, string Confirmation);
