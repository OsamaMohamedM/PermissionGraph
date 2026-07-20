namespace PermissionGraph.Application.Features.Memberships;

public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId);
