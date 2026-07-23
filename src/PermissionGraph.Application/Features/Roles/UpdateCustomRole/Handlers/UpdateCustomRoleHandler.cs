namespace PermissionGraph.Application.Features.Roles.UpdateCustomRole.Handlers;

public sealed class UpdateCustomRoleHandler(
    IValidator<UpdateCustomRoleCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IRoleRepository roleRepository,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<RoleResult> HandleAsync(UpdateCustomRoleCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var role = await roleCatalogAccess.RequireOwnerVisibleRoleAsync(command.OrganizationId, command.RoleId, actor.UserId, cancellationToken);
        var name = command.Name.Trim();
        var normalizedName = CreateCustomRoleHandler.NormalizeName(name);

        if (await roleRepository.ActiveNormalizedNameExistsAsync(command.OrganizationId, role.ScopeType, normalizedName, role.Id, cancellationToken))
        {
            throw CreateCustomRoleHandler.DuplicateName();
        }

        var now = clock.UtcNow;
        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            role.UpdateMetadata(name, normalizedName, command.Description, command.IsRequestable, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "role.updated", "Role", role.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleResult.FromDomain(role);
    }
}
