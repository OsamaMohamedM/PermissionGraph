namespace PermissionGraph.Application.Features.RoleAssignments.GetRoleAssignment.Models;

public sealed record GetRoleAssignmentQuery(Guid OrganizationId, Guid AssignmentId);
