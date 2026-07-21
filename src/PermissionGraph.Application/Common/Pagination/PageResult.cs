namespace PermissionGraph.Application.Common.Pagination;

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);