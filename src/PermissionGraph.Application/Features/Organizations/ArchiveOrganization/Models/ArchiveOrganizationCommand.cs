namespace PermissionGraph.Application.Features.Organizations.ArchiveOrganization.Models;

public sealed record ArchiveOrganizationCommand(Guid OrganizationId, string Confirmation);