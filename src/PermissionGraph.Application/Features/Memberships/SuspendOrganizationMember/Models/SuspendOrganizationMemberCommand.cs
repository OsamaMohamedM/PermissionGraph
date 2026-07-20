namespace PermissionGraph.Application.Features.Memberships;

public sealed record SuspendOrganizationMemberCommand(Guid OrganizationId, Guid UserId);
