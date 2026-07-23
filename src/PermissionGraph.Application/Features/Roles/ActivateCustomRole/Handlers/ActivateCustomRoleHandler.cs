namespace PermissionGraph.Application.Features.Roles.ActivateCustomRole.Handlers;

public sealed class ActivateCustomRoleHandler(
    IValidator<ActivateCustomRoleCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    RoleCatalogAccessHelper roleCatalogAccess,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task HandleAsync(ActivateCustomRoleCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var role = await roleCatalogAccess.RequireOwnerVisibleRoleAsync(command.OrganizationId, command.RoleId, actor.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            role.Activate(now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "role.activated", "Role", role.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }
}
