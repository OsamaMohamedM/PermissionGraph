namespace PermissionGraph.Application.Abstractions.Services.Authorization;

public interface IAuthorizationReadService
{
    Task<AuthorizationEvaluationReadModel> LoadEvaluationAsync(
        AuthorizationEvaluationReadRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuthorizationEvaluationReadModel>> LoadBatchEvaluationAsync(
        IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
        CancellationToken cancellationToken);
}

public sealed record AuthorizationEvaluationReadRequest(
    Guid SubjectUserId,
    Guid OrganizationId,
    Guid? ProjectId,
    string NormalizedPermissionKey);

public sealed record AuthorizationEvaluationReadModel(
    AuthorizationEvaluationReadRequest Request,
    AuthorizationOrganizationReadModel? Organization,
    AuthorizationPermissionReadModel? Permission,
    AuthorizationProjectReadModel? Project,
    AuthorizationMembershipReadModel? SubjectMembership,
    IReadOnlyList<RoleAssignmentPermissionPathReadModel> RoleAssignmentPermissionPaths,
    IReadOnlyList<ProjectAdministratorPermissionPathReadModel> ProjectAdministratorPermissionPaths);

public sealed record AuthorizationOrganizationReadModel(
    Guid Id,
    Guid OwnerUserId,
    bool IsActive,
    long PolicyVersion = 1);

public sealed record AuthorizationPermissionReadModel(
    Guid Id,
    Guid? OrganizationId,
    string NormalizedKey,
    PermissionType PermissionType,
    PermissionAllowedScopes AllowedScopes,
    bool IsActive);

public sealed record AuthorizationProjectReadModel(
    Guid Id,
    Guid OrganizationId,
    bool IsActive);

public sealed record AuthorizationMembershipReadModel(
    Guid OrganizationId,
    Guid UserId,
    bool IsActive,
    long AuthorizationVersion = 1);

public sealed record RoleAssignmentPermissionPathReadModel(
    Guid AssignmentOrganizationId,
    Guid AssignmentUserId,
    Guid AssignmentRoleId,
    RoleAssignmentScopeType AssignmentScopeType,
    Guid AssignmentScopeId,
    RoleAssignmentStatus AssignmentStatus,
    DateTimeOffset AssignmentStartsAtUtc,
    DateTimeOffset? AssignmentExpiresAtUtc,
    bool RoleIsActive,
    RoleScopeType RoleScopeType,
    Guid PermissionId,
    string PermissionNormalizedKey,
    PermissionAllowedScopes PermissionAllowedScopes,
    bool PermissionIsActive);

public sealed record ProjectAdministratorPermissionPathReadModel(
    Guid AssignmentOrganizationId,
    Guid AssignmentProjectId,
    Guid AssignmentUserId,
    Guid RoleId,
    bool RoleIsActive,
    RoleScopeType RoleScopeType,
    Guid PermissionId,
    string PermissionNormalizedKey,
    PermissionAllowedScopes PermissionAllowedScopes,
    bool PermissionIsActive);
