namespace PermissionGraph.Infrastructure.AuthorizationSeed;

public sealed class RolePermissionRecord
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }

    public Guid AddedByUserId { get; set; }
}
