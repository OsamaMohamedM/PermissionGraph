namespace PermissionGraph.Application.Features.Organizations.CreateOrganization.Handlers;

public sealed class CreateOrganizationHandler(
    IValidator<CreateOrganizationCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    IOrganizationRepository organizationRepository,
    IOrganizationMembershipRepository membershipRepository,
    IOrganizationSeedService seedService,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IGuidProvider guidProvider,
    IClock clock)
{
    public async Task<OrganizationResult> HandleAsync(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var now = clock.UtcNow;

        var organization = Organization.Create(
            guidProvider.NewGuid(),
            command.Name,
            NormalizeName(command.Name),
            command.Description,
            actor.UserId,
            now);

        var membership = OrganizationMembership.CreateActive(
            guidProvider.NewGuid(),
            organization.Id,
            actor.UserId,
            now,
            now);

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        await organizationRepository.AddAsync(organization, cancellationToken);
        await membershipRepository.AddAsync(membership, cancellationToken);
        await seedService.SeedDefaultAuthorizationAsync(organization, actor.UserId, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization.created", "Organization", organization.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }

    internal static string NormalizeName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }
}