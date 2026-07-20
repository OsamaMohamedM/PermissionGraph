namespace PermissionGraph.Application.Features.Memberships;

public sealed record GetOrganizationMemberQuery(Guid OrganizationId, Guid UserId);
