namespace PermissionGraph.Application.Features.Organizations.UpdateOrganization.Models;

public sealed record UpdateOrganizationCommand(Guid OrganizationId, string Name, string? Description);