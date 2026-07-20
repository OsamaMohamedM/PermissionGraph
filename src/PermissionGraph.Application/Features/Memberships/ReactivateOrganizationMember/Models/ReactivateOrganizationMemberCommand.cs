namespace PermissionGraph.Application.Features.Memberships;

public sealed record ReactivateOrganizationMemberCommand(Guid OrganizationId, Guid UserId);
