namespace PermissionGraph.Application.Features.Organizations.ListOrganizations.Models;

public sealed record ListOrganizationsQuery(int PageSize = 20, string? Cursor = null);