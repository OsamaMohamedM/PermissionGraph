using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Identifiers;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

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

public sealed class ListOrganizationsHandler(
    IValidator<ListOrganizationsQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    IOrganizationRepository organizationRepository)
{
    public async Task<PagedResult<OrganizationResult>> HandleAsync(ListOrganizationsQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var result = await organizationRepository.ListForUserAsync(actor.UserId, query.PageSize, query.Cursor, cancellationToken);

        return new PagedResult<OrganizationResult>(
            result.Items.Select(OrganizationResult.FromDomain).ToArray(),
            result.NextCursor);
    }
}

public sealed class GetOrganizationHandler(
    IValidator<GetOrganizationQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess)
{
    public async Task<OrganizationResult> HandleAsync(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }
}
