namespace PermissionGraph.Application.Features.Memberships.ReactivateOrganizationMember.Models;

public sealed record ReactivateOrganizationMemberCommand(Guid OrganizationId, Guid UserId);