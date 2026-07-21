namespace PermissionGraph.Application.Features.Permissions.UpdateCustomPermission.Handlers;

public sealed class UpdateCustomPermissionHandler(
    IValidator<UpdateCustomPermissionCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccessHelper permissionCatalogAccess,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<PermissionResult> HandleAsync(
        UpdateCustomPermissionCommand command,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var permission = await permissionCatalogAccess.RequireOwnedCustomPermissionAsync(
            command.OrganizationId,
            command.PermissionId,
            actor.UserId,
            cancellationToken);
        var shouldIncrementPolicyVersion = permission.IsRequestable != command.IsRequestable;
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            permission.UpdateMetadata(command.DisplayName, command.Description, command.Module, command.IsRequestable, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        if (shouldIncrementPolicyVersion)
        {
            await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "permission.updated", "PermissionDefinition", permission.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return PermissionResult.FromDomain(permission);
    }
}