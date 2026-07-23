namespace PermissionGraph.Application.Features.Roles.CloneRole.Handlers;

public sealed class CloneRoleHandler(
    IValidator<CloneRoleCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IRoleRepository roleRepository,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<RoleResult> HandleAsync(CloneRoleCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var sourceRole = await roleCatalogAccess.RequireOwnerVisibleRoleAsync(command.OrganizationId, command.SourceRoleId, actor.UserId, cancellationToken);
        var name = command.Name.Trim();
        var normalizedName = CreateCustomRoleHandler.NormalizeName(name);

        if (await roleRepository.ActiveNormalizedNameExistsAsync(command.OrganizationId, sourceRole.ScopeType, normalizedName, excludingRoleId: null, cancellationToken))
        {
            throw CreateCustomRoleHandler.DuplicateName();
        }

        var now = clock.UtcNow;
        Role clone;
        try
        {
            clone = sourceRole.CloneAsCustom(guidProvider.NewGuid(), name, normalizedName, command.Description, command.IsRequestable, now, actor.UserId);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await roleRepository.AddAsync(clone, cancellationToken);
        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "role.cloned", "Role", clone.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleResult.FromDomain(clone);
    }
}
