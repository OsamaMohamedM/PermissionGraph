namespace PermissionGraph.Application.Features.Roles.ReplaceRolePermissions.Handlers;

public sealed class ReplaceRolePermissionsHandler(
    IValidator<ReplaceRolePermissionsCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IPermissionDefinitionRepository permissionRepository,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<RoleResult> HandleAsync(ReplaceRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var role = await roleCatalogAccess.RequireOwnerVisibleRoleAsync(command.OrganizationId, command.RoleId, actor.UserId, cancellationToken);

        if (command.PermissionIds.Count != command.PermissionIds.Distinct().Count())
        {
            throw new ConflictApplicationException("role_permission_duplicate", "Role permission mappings cannot contain duplicate permission identifiers.");
        }

        if (IsSamePermissionSet(role, command.PermissionIds))
        {
            return RoleResult.FromDomain(role);
        }

        var permissions = await CreateCustomRoleHandler.LoadVisiblePermissionsAsync(
            permissionRepository,
            command.OrganizationId,
            command.PermissionIds,
            cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            role.ReplacePermissions(permissions, actor.UserId, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "role.permissions_updated", "Role", role.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleResult.FromDomain(role);
    }

    private static bool IsSamePermissionSet(Role role, IReadOnlyCollection<Guid> permissionIds)
    {
        return role.Permissions.Select(permission => permission.PermissionId).Order().SequenceEqual(permissionIds.Order());
    }
}
