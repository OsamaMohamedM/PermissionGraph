namespace PermissionGraph.Application.Features.Permissions.ActivateCustomPermission.Handlers;

public sealed class ActivateCustomPermissionHandler(
    IValidator<ActivateCustomPermissionCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccessHelper permissionCatalogAccess,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task HandleAsync(ActivateCustomPermissionCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var permission = await permissionCatalogAccess.RequireOwnedCustomPermissionAsync(
            command.OrganizationId,
            command.PermissionId,
            actor.UserId,
            cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            permission.Activate(now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "permission.activated", "PermissionDefinition", permission.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }
}
