namespace PermissionGraph.Application.Features.Authorization.ExplainAccess.Models;

public sealed record ExplainAccessQuery(
    Guid? SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string PermissionKey,
    DateTimeOffset? EvaluatedAtUtc = null)
{
    public string NormalizedPermissionKey => CheckPermissionQuery.NormalizePermissionKey(PermissionKey);
}
