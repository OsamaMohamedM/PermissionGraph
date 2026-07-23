namespace PermissionGraph.Application.Features.Roles.CloneRole.Models;

public sealed record CloneRoleCommand(
    Guid OrganizationId,
    Guid SourceRoleId,
    string Name,
    string? Description,
    bool IsRequestable);
