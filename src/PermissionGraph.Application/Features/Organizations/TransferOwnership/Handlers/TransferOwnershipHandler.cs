using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Security;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Domain.Common;
using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class TransferOwnershipHandler(
    IValidator<TransferOwnershipCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IUserAccountLookup userAccountLookup,
    IRecentAuthenticationVerifier recentAuthenticationVerifier,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<OrganizationResult> HandleAsync(TransferOwnershipCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);

        if (!await recentAuthenticationVerifier.HasRecentAuthenticationAsync(actor.UserId, command.CurrentPassword, cancellationToken))
        {
            throw new ForbiddenApplicationException("recent_authentication_required", "Recent authentication is required.");
        }

        var targetAccount = await userAccountLookup.FindByIdAsync(command.NewOwnerUserId, cancellationToken);
        var targetMembership = await membershipRepository.GetByOrganizationAndUserAsync(
            organization.Id,
            command.NewOwnerUserId,
            cancellationToken);

        if (targetAccount is null || !targetAccount.IsActive || targetMembership is null || targetMembership.Status != MembershipStatus.Active)
        {
            throw new ConflictApplicationException("target_owner_must_be_active_member", "Target owner must be an active organization member.");
        }

        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            organization.TransferOwnership(command.NewOwnerUserId, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await membershipRepository.IncrementAuthorizationVersionAsync(organization.Id, actor.UserId, now, cancellationToken);
        await membershipRepository.IncrementAuthorizationVersionAsync(organization.Id, command.NewOwnerUserId, now, cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization.ownership_transferred", "Organization", organization.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationResult.FromDomain(organization);
    }
}
