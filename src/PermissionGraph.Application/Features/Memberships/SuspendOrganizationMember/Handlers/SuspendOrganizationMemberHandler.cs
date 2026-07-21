namespace PermissionGraph.Application.Features.Memberships.SuspendOrganizationMember.Handlers;

public sealed class SuspendOrganizationMemberHandler(
    IValidator<SuspendOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<OrganizationMemberResult> HandleAsync(SuspendOrganizationMemberCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var membership = await GetTargetMembershipAsync(membershipRepository, organization.Id, command.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            membership.Suspend(organization.OwnerUserId == command.UserId, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization_member.suspended", "OrganizationMembership", membership.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationMemberResult.FromDomain(membership);
    }
}