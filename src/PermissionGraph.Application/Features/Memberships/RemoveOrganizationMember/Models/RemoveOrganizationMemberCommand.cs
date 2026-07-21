namespace PermissionGraph.Application.Features.Memberships.RemoveOrganizationMember.Models;

public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId);