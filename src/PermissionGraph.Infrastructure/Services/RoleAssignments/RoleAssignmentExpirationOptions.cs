namespace PermissionGraph.Infrastructure.Services.RoleAssignments;

public sealed class RoleAssignmentExpirationOptions
{
    public int BatchSize { get; init; } = 100;

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);
}
