namespace PermissionGraph.Application.Abstractions.Services.Authorization;

public interface IAccessExplanationReadService
{
    Task<AccessExplanationReadModel> LoadAsync(
        AccessExplanationReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record AccessExplanationReadRequest(
    Guid SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string NormalizedPermissionKey);

public sealed record AccessExplanationReadModel(
    AccessExplanationReadRequest Request,
    AuthorizationOrganizationReadModel? Organization,
    AuthorizationPermissionReadModel? Permission,
    AuthorizationProjectReadModel? Project,
    AuthorizationMembershipReadModel? SubjectMembership,
    IReadOnlyList<AccessExplanationRoleAssignmentReadModel> RoleAssignments,
    IReadOnlyList<AccessExplanationProjectAdministratorReadModel> ProjectAdministratorAssignments);

public sealed record AccessExplanationRoleAssignmentReadModel(
    Guid AssignmentId,
    Guid AssignmentOrganizationId,
    Guid AssignmentUserId,
    Guid AssignmentRoleId,
    RoleAssignmentScopeType AssignmentScopeType,
    Guid AssignmentScopeId,
    RoleAssignmentStatus AssignmentStatus,
    DateTimeOffset AssignmentStartsAtUtc,
    DateTimeOffset? AssignmentExpiresAtUtc,
    string RoleName,
    bool RoleIsActive,
    RoleScopeType RoleScopeType,
    bool RoleContainsPermission,
    Guid? MatchedPermissionId,
    string? MatchedPermissionNormalizedKey,
    PermissionAllowedScopes? MatchedPermissionAllowedScopes,
    bool? MatchedPermissionIsActive);

public sealed record AccessExplanationProjectAdministratorReadModel(
    Guid AssignmentOrganizationId,
    Guid AssignmentProjectId,
    Guid AssignmentUserId,
    Guid RoleId,
    string RoleName,
    bool RoleIsActive,
    RoleScopeType RoleScopeType,
    bool RoleContainsPermission,
    Guid? MatchedPermissionId,
    string? MatchedPermissionNormalizedKey,
    PermissionAllowedScopes? MatchedPermissionAllowedScopes,
    bool? MatchedPermissionIsActive);
