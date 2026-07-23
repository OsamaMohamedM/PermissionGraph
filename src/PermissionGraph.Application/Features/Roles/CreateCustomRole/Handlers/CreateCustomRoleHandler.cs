namespace PermissionGraph.Application.Features.Roles.CreateCustomRole.Handlers;

public sealed class CreateCustomRoleHandler(
    IValidator<CreateCustomRoleCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IPermissionDefinitionRepository permissionRepository,
    IRoleRepository roleRepository,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<RoleResult> HandleAsync(CreateCustomRoleCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await roleCatalogAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var name = command.Name.Trim();
        var normalizedName = NormalizeName(name);

        if (await roleRepository.ActiveNormalizedNameExistsAsync(organization.Id, command.ScopeType, normalizedName, excludingRoleId: null, cancellationToken))
        {
            throw DuplicateName();
        }

        var permissions = await LoadVisiblePermissionsAsync(
            permissionRepository,
            organization.Id,
            command.PermissionIds,
            cancellationToken);
        var now = clock.UtcNow;
        Role role;
        try
        {
            role = Role.CreateCustom(
                guidProvider.NewGuid(),
                organization.Id,
                name,
                normalizedName,
                command.Description,
                command.ScopeType,
                command.IsRequestable,
                now,
                permissions,
                actor.UserId);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await roleRepository.AddAsync(role, cancellationToken);
        await policyVersionUpdater.IncrementPolicyVersionAsync(organization.Id, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "role.created", "Role", role.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleResult.FromDomain(role);
    }

    internal static string NormalizeName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }

    internal static ConflictApplicationException DuplicateName()
    {
        return new ConflictApplicationException("role_name_already_exists", "An active role with this name already exists in this organization and scope.");
    }

    internal static async Task<IReadOnlyCollection<PermissionDefinition>> LoadVisiblePermissionsAsync(
        IPermissionDefinitionRepository permissionRepository,
        Guid organizationId,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var permissions = new List<PermissionDefinition>(permissionIds.Count);
        foreach (var permissionId in permissionIds)
        {
            var permission = await permissionRepository.GetVisibleByOrganizationAndIdAsync(
                organizationId,
                permissionId,
                cancellationToken);

            if (permission is null)
            {
                throw new NotFoundApplicationException("permission_not_found", "Permission could not be found.");
            }

            permissions.Add(permission);
        }

        return permissions;
    }
}
