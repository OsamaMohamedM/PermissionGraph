namespace PermissionGraph.Application.Features.Memberships.SuspendOrganizationMember.Models;

public sealed record SuspendOrganizationMemberCommand(Guid OrganizationId, Guid UserId);