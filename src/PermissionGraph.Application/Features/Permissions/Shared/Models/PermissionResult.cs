using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Application.Features.Permissions;

public sealed record PermissionResult(
    Guid Id,
    Guid? OrganizationId,
    string Key,
    string NormalizedKey,
    string DisplayName,
    string? Description,
    string Module,
    PermissionType PermissionType,
    PermissionAllowedScopes AllowedScopes,
    bool IsRequestable,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    uint Version)
{
    public static PermissionResult FromDomain(PermissionDefinition permission)
    {
        return new PermissionResult(
            permission.Id,
            permission.OrganizationId,
            permission.Key,
            permission.NormalizedKey,
            permission.DisplayName,
            permission.Description,
            permission.Module,
            permission.PermissionType,
            permission.AllowedScopes,
            permission.IsRequestable,
            permission.IsActive,
            permission.CreatedAtUtc,
            permission.UpdatedAtUtc,
            permission.ArchivedAtUtc,
            permission.Version);
    }
}
