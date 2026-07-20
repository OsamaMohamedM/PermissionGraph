namespace PermissionGraph.Application.Features.Memberships;

public sealed record AddOrganizationMemberCommand(Guid OrganizationId, string Email);
