namespace PermissionGraph.Application.Features.Organizations.UpdateOrganization.Handlers;

public sealed class UpdateOrganizationHandler(
    IValidator<UpdateOrganizationCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<OrganizationResult> HandleAsync(UpdateOrganizationCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            organization.UpdateDetails(command.Name, CreateOrganizationHandler.NormalizeName(command.Name), command.Description, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization.updated", "Organization", organization.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }
}