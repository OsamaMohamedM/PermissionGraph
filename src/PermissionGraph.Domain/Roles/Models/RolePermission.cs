namespace PermissionGraph.Domain.Roles.Models;

public sealed class RolePermission
{
    private RolePermission(
        Guid roleId,
        Guid permissionId,
        DateTimeOffset addedAtUtc,
        Guid addedByUserId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        AddedAtUtc = addedAtUtc;
        AddedByUserId = addedByUserId;
    }

    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public Guid AddedByUserId { get; private set; }

    internal static RolePermission Create(
        Guid roleId,
        Guid permissionId,
        DateTimeOffset addedAtUtc,
        Guid addedByUserId)
    {
        EnsureNotEmpty(roleId, nameof(roleId));
        EnsureNotEmpty(permissionId, nameof(permissionId));
        EnsureNotEmpty(addedByUserId, nameof(addedByUserId));

        return new RolePermission(roleId, permissionId, addedAtUtc, addedByUserId);
    }

    internal RolePermission CopyForRole(
        Guid roleId,
        DateTimeOffset addedAtUtc,
        Guid addedByUserId)
    {
        return Create(roleId, PermissionId, addedAtUtc, addedByUserId);
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invalid_identifier",
                $"{parameterName} is required.");
        }
    }
}
