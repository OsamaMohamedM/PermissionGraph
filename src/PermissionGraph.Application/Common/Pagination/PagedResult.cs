namespace PermissionGraph.Application.Common.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);
