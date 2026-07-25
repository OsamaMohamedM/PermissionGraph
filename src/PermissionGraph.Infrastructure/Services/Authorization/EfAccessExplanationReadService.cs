namespace PermissionGraph.Infrastructure.Services.Authorization;

internal sealed class EfAccessExplanationReadService(PermissionGraphDbContext dbContext) : IAccessExplanationReadService
{
    public async Task<AccessExplanationReadModel> LoadAsync(
        AccessExplanationReadRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .Where(item => item.Id == request.OrganizationId)
            .Select(item => new AuthorizationOrganizationReadModel(
                item.Id,
                item.OwnerUserId,
                item.IsActive,
                item.PolicyVersion))
            .SingleOrDefaultAsync(cancellationToken);

        var permission = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .Where(item =>
                item.NormalizedKey == request.NormalizedPermissionKey &&
                (item.PermissionType == PermissionType.Platform ||
                    item.OrganizationId == request.OrganizationId))
            .OrderByDescending(item => item.PermissionType == PermissionType.Custom)
            .Select(item => new AuthorizationPermissionReadModel(
                item.Id,
                item.OrganizationId,
                item.NormalizedKey,
                item.PermissionType,
                item.AllowedScopes,
                item.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        var project = request.ProjectId is null
            ? null
            : await dbContext.Projects
                .AsNoTracking()
                .Where(item => item.Id == request.ProjectId.Value)
                .Select(item => new AuthorizationProjectReadModel(
                    item.Id,
                    item.OrganizationId,
                    item.IsActive))
                .SingleOrDefaultAsync(cancellationToken);

        var membership = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == request.OrganizationId &&
                item.UserId == request.SubjectUserId)
            .Select(item => new AuthorizationMembershipReadModel(
                item.OrganizationId,
                item.UserId,
                item.IsActive,
                item.AuthorizationVersion))
            .SingleOrDefaultAsync(cancellationToken);

        var roleAssignments = await LoadRoleAssignmentsAsync(request, cancellationToken);
        var projectAdministratorAssignments = request.ProjectId is null
            ? []
            : await LoadProjectAdministratorAssignmentsAsync(request, cancellationToken);

        return new AccessExplanationReadModel(
            request,
            organization,
            permission,
            project,
            membership,
            roleAssignments,
            projectAdministratorAssignments);
    }

    private async Task<IReadOnlyList<AccessExplanationRoleAssignmentReadModel>> LoadRoleAssignmentsAsync(
        AccessExplanationReadRequest request,
        CancellationToken cancellationToken)
    {
        var assignments = await (
            from assignment in dbContext.RoleAssignments.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            where assignment.OrganizationId == request.OrganizationId &&
                assignment.UserId == request.SubjectUserId
            orderby assignment.StartsAtUtc
            select new RoleAssignmentExplanationProjection(
                assignment.Id,
                assignment.OrganizationId,
                assignment.UserId,
                assignment.RoleId,
                assignment.ScopeType,
                assignment.ScopeId,
                assignment.Status,
                assignment.StartsAtUtc,
                assignment.ExpiresAtUtc,
                role.Name,
                role.IsActive,
                role.ScopeType))
            .ToListAsync(cancellationToken);

        var permissionMatches = await LoadRolePermissionMatchesAsync(
            request.OrganizationId,
            assignments.Select(assignment => assignment.RoleId).Distinct().ToArray(),
            request.NormalizedPermissionKey,
            cancellationToken);

        return assignments
            .Select(assignment =>
            {
                permissionMatches.TryGetValue(assignment.RoleId, out var permission);
                return new AccessExplanationRoleAssignmentReadModel(
                    assignment.AssignmentId,
                    assignment.OrganizationId,
                    assignment.UserId,
                    assignment.RoleId,
                    assignment.ScopeType,
                    assignment.ScopeId,
                    assignment.Status,
                    assignment.StartsAtUtc,
                    assignment.ExpiresAtUtc,
                    assignment.RoleName,
                    assignment.RoleIsActive,
                    assignment.RoleScopeType,
                    permission is not null,
                    permission?.PermissionId,
                    permission?.NormalizedKey,
                    permission?.AllowedScopes,
                    permission?.IsActive);
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AccessExplanationProjectAdministratorReadModel>> LoadProjectAdministratorAssignmentsAsync(
        AccessExplanationReadRequest request,
        CancellationToken cancellationToken)
    {
        var assignments = await (
            from assignment in dbContext.ProjectAdministratorAssignments.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            where assignment.OrganizationId == request.OrganizationId &&
                assignment.ProjectId == request.ProjectId!.Value &&
                assignment.UserId == request.SubjectUserId
            orderby assignment.CreatedAtUtc
            select new ProjectAdministratorExplanationProjection(
                assignment.OrganizationId,
                assignment.ProjectId,
                assignment.UserId,
                role.Id,
                role.Name,
                role.IsActive,
                role.ScopeType))
            .ToListAsync(cancellationToken);

        var permissionMatches = await LoadRolePermissionMatchesAsync(
            request.OrganizationId,
            assignments.Select(assignment => assignment.RoleId).Distinct().ToArray(),
            request.NormalizedPermissionKey,
            cancellationToken);

        return assignments
            .Select(assignment =>
            {
                permissionMatches.TryGetValue(assignment.RoleId, out var permission);
                return new AccessExplanationProjectAdministratorReadModel(
                    assignment.OrganizationId,
                    assignment.ProjectId,
                    assignment.UserId,
                    assignment.RoleId,
                    assignment.RoleName,
                    assignment.RoleIsActive,
                    assignment.RoleScopeType,
                    permission?.IsActive == true,
                    permission?.PermissionId,
                    permission?.NormalizedKey,
                    permission?.AllowedScopes,
                    permission?.IsActive);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, RolePermissionMatchProjection>> LoadRolePermissionMatchesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> roleIds,
        string normalizedPermissionKey,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<Guid, RolePermissionMatchProjection>();
        }

        var matches = await (
            from rolePermission in dbContext.RolePermissions.AsNoTracking()
            join permission in dbContext.PermissionDefinitions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where roleIds.Contains(rolePermission.RoleId) &&
                permission.NormalizedKey == normalizedPermissionKey &&
                (permission.PermissionType == PermissionType.Platform ||
                    permission.OrganizationId == organizationId)
            select new RolePermissionMatchProjection(
                rolePermission.RoleId,
                permission.Id,
                permission.NormalizedKey,
                permission.PermissionType,
                permission.AllowedScopes,
                permission.IsActive))
            .ToListAsync(cancellationToken);

        return matches
            .GroupBy(match => match.RoleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(match => match.PermissionType == PermissionType.Custom)
                    .First());
    }

    private sealed record RoleAssignmentExplanationProjection(
        Guid AssignmentId,
        Guid OrganizationId,
        Guid UserId,
        Guid RoleId,
        RoleAssignmentScopeType ScopeType,
        Guid ScopeId,
        RoleAssignmentStatus Status,
        DateTimeOffset StartsAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        string RoleName,
        bool RoleIsActive,
        RoleScopeType RoleScopeType);

    private sealed record ProjectAdministratorExplanationProjection(
        Guid OrganizationId,
        Guid ProjectId,
        Guid UserId,
        Guid RoleId,
        string RoleName,
        bool RoleIsActive,
        RoleScopeType RoleScopeType);

    private sealed record RolePermissionMatchProjection(
        Guid RoleId,
        Guid PermissionId,
        string NormalizedKey,
        PermissionType PermissionType,
        PermissionAllowedScopes AllowedScopes,
        bool IsActive);
}
