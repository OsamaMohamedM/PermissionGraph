namespace PermissionGraph.Application.Features.Authorization.CheckPermission.Models;

public sealed record CheckPermissionQuery(
    Guid? SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string PermissionKey,
    DateTimeOffset? RequestedEvaluationTimeUtc = null)
{
    public AuthorizationScope Scope => new(OrganizationId, ProjectId);

    public string NormalizedPermissionKey => NormalizePermissionKey(PermissionKey);

    internal static string NormalizePermissionKey(string permissionKey)
    {
        return permissionKey.Trim().ToLowerInvariant();
    }
}
