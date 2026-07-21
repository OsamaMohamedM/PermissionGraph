namespace PermissionGraph.Application.Features.Organizations.CreateOrganization.Models;

public sealed record CreateOrganizationCommand(string Name, string? Description);