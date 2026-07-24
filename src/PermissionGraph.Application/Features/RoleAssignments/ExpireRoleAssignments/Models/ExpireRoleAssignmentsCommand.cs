namespace PermissionGraph.Application.Features.RoleAssignments.ExpireRoleAssignments.Models;

public sealed record ExpireRoleAssignmentsCommand(DateTimeOffset NowUtc, int BatchSize);

public sealed record ExpireRoleAssignmentsResult(int ExpiredCount);
