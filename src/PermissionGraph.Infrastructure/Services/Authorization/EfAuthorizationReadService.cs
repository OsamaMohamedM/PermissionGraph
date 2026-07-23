namespace PermissionGraph.Infrastructure.Services.Authorization;

internal sealed class EfAuthorizationReadService(PermissionGraphDbContext dbContext) : IAuthorizationReadService
{
    public async Task<AuthorizationEvaluationReadModel> LoadEvaluationAsync(
        AuthorizationEvaluationReadRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await LoadOrganizationsAsync([request.OrganizationId], cancellationToken);
        var permissions = await LoadPermissionsAsync([request], cancellationToken);
        var projects = request.ProjectId is null
            ? new Dictionary<Guid, AuthorizationProjectReadModel>()
            : await LoadProjectsAsync([request.ProjectId.Value], cancellationToken);
        var memberships = await LoadMembershipsAsync([request], cancellationToken);
        var paths = request.ProjectId is null
            ? []
            : await LoadProjectAdministratorPermissionPathsAsync([request], cancellationToken);

        return BuildReadModel(request, organization, permissions, projects, memberships, paths);
    }

    public async Task<IReadOnlyList<AuthorizationEvaluationReadModel>> LoadBatchEvaluationAsync(
        IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var organizationIds = requests
            .Select(request => request.OrganizationId)
            .Distinct()
            .ToArray();
        var projectIds = requests
            .Where(request => request.ProjectId is not null)
            .Select(request => request.ProjectId!.Value)
            .Distinct()
            .ToArray();

        var organizations = await LoadOrganizationsAsync(organizationIds, cancellationToken);
        var permissions = await LoadPermissionsAsync(requests, cancellationToken);
        var projects = projectIds.Length == 0
            ? new Dictionary<Guid, AuthorizationProjectReadModel>()
            : await LoadProjectsAsync(projectIds, cancellationToken);
        var memberships = await LoadMembershipsAsync(requests, cancellationToken);
        var paths = projectIds.Length == 0
            ? []
            : await LoadProjectAdministratorPermissionPathsAsync(requests, cancellationToken);

        return requests
            .Select(request => BuildReadModel(request, organizations, permissions, projects, memberships, paths))
            .ToArray();
    }

    private async Task<Dictionary<Guid, AuthorizationOrganizationReadModel>> LoadOrganizationsAsync(
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organizationIds.Contains(organization.Id))
            .Select(organization => new AuthorizationOrganizationReadModel(
                organization.Id,
                organization.OwnerUserId,
                organization.IsActive))
            .ToDictionaryAsync(organization => organization.Id, cancellationToken);
    }

    private async Task<Dictionary<PermissionLookupKey, AuthorizationPermissionReadModel>> LoadPermissionsAsync(
        IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
        CancellationToken cancellationToken)
    {
        var organizationIds = requests
            .Select(request => request.OrganizationId)
            .Distinct()
            .ToArray();
        var normalizedPermissionKeys = requests
            .Select(request => request.NormalizedPermissionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var permissionRows = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .Where(permission =>
                normalizedPermissionKeys.Contains(permission.NormalizedKey) &&
                (permission.PermissionType == PermissionType.Platform ||
                    (permission.OrganizationId != null && organizationIds.Contains(permission.OrganizationId.Value))))
            .Select(permission => new AuthorizationPermissionReadModel(
                permission.Id,
                permission.OrganizationId,
                permission.NormalizedKey,
                permission.PermissionType,
                permission.AllowedScopes,
                permission.IsActive))
            .ToListAsync(cancellationToken);

        var permissions = new Dictionary<PermissionLookupKey, AuthorizationPermissionReadModel>();
        foreach (var request in requests)
        {
            var permission = permissionRows
                .Where(row =>
                    string.Equals(row.NormalizedKey, request.NormalizedPermissionKey, StringComparison.Ordinal) &&
                    (row.PermissionType == PermissionType.Platform || row.OrganizationId == request.OrganizationId))
                .OrderByDescending(row => row.PermissionType == PermissionType.Custom)
                .FirstOrDefault();

            if (permission is not null)
            {
                permissions[new PermissionLookupKey(request.OrganizationId, request.NormalizedPermissionKey)] = permission;
            }
        }

        return permissions;
    }

    private async Task<Dictionary<Guid, AuthorizationProjectReadModel>> LoadProjectsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Projects
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .Select(project => new AuthorizationProjectReadModel(
                project.Id,
                project.OrganizationId,
                project.IsActive))
            .ToDictionaryAsync(project => project.Id, cancellationToken);
    }

    private async Task<Dictionary<MembershipLookupKey, AuthorizationMembershipReadModel>> LoadMembershipsAsync(
        IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
        CancellationToken cancellationToken)
    {
        var organizationIds = requests
            .Select(request => request.OrganizationId)
            .Distinct()
            .ToArray();
        var subjectUserIds = requests
            .Select(request => request.SubjectUserId)
            .Distinct()
            .ToArray();

        var memberships = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership =>
                organizationIds.Contains(membership.OrganizationId) &&
                subjectUserIds.Contains(membership.UserId))
            .Select(membership => new AuthorizationMembershipReadModel(
                membership.OrganizationId,
                membership.UserId,
                membership.IsActive))
            .ToListAsync(cancellationToken);

        return memberships.ToDictionary(
            membership => new MembershipLookupKey(membership.OrganizationId, membership.UserId),
            membership => membership);
    }

    private async Task<IReadOnlyList<ProjectAdministratorPermissionPathReadModel>> LoadProjectAdministratorPermissionPathsAsync(
        IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
        CancellationToken cancellationToken)
    {
        var projectRequests = requests
            .Where(request => request.ProjectId is not null)
            .ToArray();
        var organizationIds = projectRequests
            .Select(request => request.OrganizationId)
            .Distinct()
            .ToArray();
        var projectIds = projectRequests
            .Select(request => request.ProjectId!.Value)
            .Distinct()
            .ToArray();
        var subjectUserIds = projectRequests
            .Select(request => request.SubjectUserId)
            .Distinct()
            .ToArray();
        var normalizedPermissionKeys = projectRequests
            .Select(request => request.NormalizedPermissionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return await (
            from assignment in dbContext.ProjectAdministratorAssignments.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on role.Id equals rolePermission.RoleId
            join permission in dbContext.PermissionDefinitions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where organizationIds.Contains(assignment.OrganizationId) &&
                projectIds.Contains(assignment.ProjectId) &&
                subjectUserIds.Contains(assignment.UserId) &&
                role.IsActive &&
                role.ScopeType == RoleScopeType.Project &&
                normalizedPermissionKeys.Contains(permission.NormalizedKey) &&
                permission.IsActive &&
                (permission.AllowedScopes == PermissionAllowedScopes.Project ||
                    permission.AllowedScopes == PermissionAllowedScopes.OrganizationAndProject) &&
                (permission.PermissionType == PermissionType.Platform ||
                    permission.OrganizationId == assignment.OrganizationId)
            select new ProjectAdministratorPermissionPathReadModel(
                assignment.OrganizationId,
                assignment.ProjectId,
                assignment.UserId,
                role.Id,
                role.IsActive,
                role.ScopeType,
                permission.Id,
                permission.NormalizedKey,
                permission.AllowedScopes,
                permission.IsActive))
            .ToListAsync(cancellationToken);
    }

    private static AuthorizationEvaluationReadModel BuildReadModel(
        AuthorizationEvaluationReadRequest request,
        IReadOnlyDictionary<Guid, AuthorizationOrganizationReadModel> organizations,
        IReadOnlyDictionary<PermissionLookupKey, AuthorizationPermissionReadModel> permissions,
        IReadOnlyDictionary<Guid, AuthorizationProjectReadModel> projects,
        IReadOnlyDictionary<MembershipLookupKey, AuthorizationMembershipReadModel> memberships,
        IReadOnlyList<ProjectAdministratorPermissionPathReadModel> paths)
    {
        organizations.TryGetValue(request.OrganizationId, out var organization);
        permissions.TryGetValue(new PermissionLookupKey(request.OrganizationId, request.NormalizedPermissionKey), out var permission);
        var project = request.ProjectId is null || !projects.TryGetValue(request.ProjectId.Value, out var foundProject)
            ? null
            : foundProject;
        memberships.TryGetValue(new MembershipLookupKey(request.OrganizationId, request.SubjectUserId), out var membership);

        var matchingPaths = request.ProjectId is null
            ? []
            : paths
                .Where(path =>
                    path.AssignmentOrganizationId == request.OrganizationId &&
                    path.AssignmentProjectId == request.ProjectId.Value &&
                    path.AssignmentUserId == request.SubjectUserId &&
                    string.Equals(path.PermissionNormalizedKey, request.NormalizedPermissionKey, StringComparison.Ordinal))
                .ToArray();

        return new AuthorizationEvaluationReadModel(
            request,
            organization,
            permission,
            project,
            membership,
            matchingPaths);
    }

    private sealed record PermissionLookupKey(Guid OrganizationId, string NormalizedPermissionKey);

    private sealed record MembershipLookupKey(Guid OrganizationId, Guid UserId);
}
