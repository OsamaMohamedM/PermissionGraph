namespace PermissionGraph.Application.Features.Organizations;

public sealed record ListOrganizationsQuery(int PageSize = 20, string? Cursor = null);
