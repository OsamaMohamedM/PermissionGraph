namespace PermissionGraph.Application.Features.Memberships.AddOrganizationMember.Models;

public sealed record AddOrganizationMemberCommand(Guid OrganizationId, string Email);