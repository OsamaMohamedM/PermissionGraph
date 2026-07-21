namespace PermissionGraph.Application.Features.Memberships.ListOrganizationMembers.Models;

public sealed record ListOrganizationMembersQuery(
    Guid OrganizationId,
    int PageSize = 20,
    string? Cursor = null,
    string? Search = null,
    string? Status = null);