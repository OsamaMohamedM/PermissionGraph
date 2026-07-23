namespace PermissionGraph.Application.Features.Authorization.BatchCheckPermissions.Models;

public sealed record BatchCheckPermissionItem(
    string CorrelationId,
    Guid? SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string PermissionKey,
    DateTimeOffset? RequestedEvaluationTimeUtc = null)
{
    public CheckPermissionQuery ToCheckPermissionQuery()
    {
        return new CheckPermissionQuery(
            SubjectUserId,
            OrganizationId,
            ProjectId,
            PermissionKey,
            RequestedEvaluationTimeUtc);
    }
}
