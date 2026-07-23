namespace PermissionGraph.Application.Features.Authorization.BatchCheckPermissions.Models;

public sealed record BatchCheckPermissionsQuery(IReadOnlyList<BatchCheckPermissionItem> Checks)
{
    public const int MaxChecks = 50;

    public IReadOnlyList<BatchCheckPermissionItem> OrderedChecks => Checks
        .Select((check, index) => new { check, index })
        .OrderBy(item => item.index)
        .Select(item => item.check)
        .ToArray();
}
