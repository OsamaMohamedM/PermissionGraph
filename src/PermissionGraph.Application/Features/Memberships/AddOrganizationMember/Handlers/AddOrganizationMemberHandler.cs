namespace PermissionGraph.Application.Features.Memberships.AddOrganizationMember.Handlers;

public sealed class AddOrganizationMemberHandler(
    IValidator<AddOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccessHelper organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IUserAccountLookup userAccountLookup,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<OrganizationMemberResult> HandleAsync(AddOrganizationMemberCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);

        var targetAccount = await userAccountLookup.FindByEmailAsync(command.Email, cancellationToken);
        if (targetAccount is null || !targetAccount.IsActive)
        {
            throw new NotFoundApplicationException("user_not_found", "User could not be found.");
        }

        var existing = await membershipRepository.GetByOrganizationAndUserIncludingRemovedAsync(
            organization.Id,
            targetAccount.UserId,
            cancellationToken);

        if (existing is not null)
        {
            throw new ConflictApplicationException("membership_already_exists", "Organization membership already exists.");
        }

        var now = clock.UtcNow;
        var membership = OrganizationMembership.CreateActive(guidProvider.NewGuid(), organization.Id, targetAccount.UserId, now, now);

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await membershipRepository.AddAsync(membership, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization_member.added", "OrganizationMembership", membership.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationMemberResult.FromDomain(membership, targetAccount.Email, targetAccount.DisplayName);
    }
}