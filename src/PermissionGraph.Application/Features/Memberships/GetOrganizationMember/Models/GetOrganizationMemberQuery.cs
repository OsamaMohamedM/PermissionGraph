namespace PermissionGraph.Application.Features.Memberships.GetOrganizationMember.Models;

public sealed record GetOrganizationMemberQuery(Guid OrganizationId, Guid UserId);