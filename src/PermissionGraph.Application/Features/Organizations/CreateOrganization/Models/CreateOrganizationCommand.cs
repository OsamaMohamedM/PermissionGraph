namespace PermissionGraph.Application.Features.Organizations;

public sealed record CreateOrganizationCommand(string Name, string? Description);
