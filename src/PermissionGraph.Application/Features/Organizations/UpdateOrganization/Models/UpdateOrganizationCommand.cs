namespace PermissionGraph.Application.Features.Organizations;

public sealed record UpdateOrganizationCommand(Guid OrganizationId, string Name, string? Description);
