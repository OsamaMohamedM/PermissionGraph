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
        return await (
            from assignment in dbContext.RoleAssignments.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            where assignment.OrganizationId == request.OrganizationId &&
                assignment.UserId == request.SubjectUserId
            orderby assignment.StartsAtUtc
            select new AccessExplanationRoleAssignmentReadModel(
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
                role.ScopeType,
                dbContext.RolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == role.Id &&
                    dbContext.PermissionDefinitions.Any(permission =>
                        permission.Id == rolePermission.PermissionId &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId))),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (Guid?)permission.Id
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select permission.NormalizedKey
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (PermissionAllowedScopes?)permission.AllowedScopes
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (bool?)permission.IsActive
                ).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AccessExplanationProjectAdministratorReadModel>> LoadProjectAdministratorAssignmentsAsync(
        AccessExplanationReadRequest request,
        CancellationToken cancellationToken)
    {
        return await (
            from assignment in dbContext.ProjectAdministratorAssignments.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            where assignment.OrganizationId == request.OrganizationId &&
                assignment.ProjectId == request.ProjectId!.Value &&
                assignment.UserId == request.SubjectUserId
            orderby assignment.CreatedAtUtc
            select new AccessExplanationProjectAdministratorReadModel(
                assignment.OrganizationId,
                assignment.ProjectId,
                assignment.UserId,
                role.Id,
                role.Name,
                role.IsActive,
                role.ScopeType,
                dbContext.RolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == role.Id &&
                    dbContext.PermissionDefinitions.Any(permission =>
                        permission.Id == rolePermission.PermissionId &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        permission.IsActive &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId))),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (Guid?)permission.Id
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select permission.NormalizedKey
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (PermissionAllowedScopes?)permission.AllowedScopes
                ).FirstOrDefault(),
                (
                    from rolePermission in dbContext.RolePermissions
                    join permission in dbContext.PermissionDefinitions
                        on rolePermission.PermissionId equals permission.Id
                    where rolePermission.RoleId == role.Id &&
                        permission.NormalizedKey == request.NormalizedPermissionKey &&
                        (permission.PermissionType == PermissionType.Platform ||
                            permission.OrganizationId == assignment.OrganizationId)
                    orderby permission.PermissionType == PermissionType.Custom descending
                    select (bool?)permission.IsActive
                ).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
