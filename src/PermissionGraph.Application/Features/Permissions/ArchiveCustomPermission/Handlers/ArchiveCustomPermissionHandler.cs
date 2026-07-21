namespace PermissionGraph.Application.Features.Permissions.ArchiveCustomPermission.Handlers;

public sealed class ArchiveCustomPermissionHandler(
    IValidator<ArchiveCustomPermissionCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    PermissionCatalogAccessHelper permissionCatalogAccess,
    IOrganizationPolicyVersionUpdater policyVersionUpdater,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task HandleAsync(ArchiveCustomPermissionCommand command, CancellationToken cancellationToken)
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
            permission.Archive(now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await policyVersionUpdater.IncrementPolicyVersionAsync(command.OrganizationId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(command.OrganizationId, actor.UserId, "permission.archived", "PermissionDefinition", permission.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }
}