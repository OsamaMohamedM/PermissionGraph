namespace PermissionGraph.Application.Features.RoleAssignments.RevokeRoleAssignment.Models;

public sealed record RevokeRoleAssignmentCommand(
    Guid OrganizationId,
    Guid AssignmentId,
    string Reason);
