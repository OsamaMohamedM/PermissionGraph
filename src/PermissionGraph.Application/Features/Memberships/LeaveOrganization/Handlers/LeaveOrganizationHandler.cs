using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Common;
using static PermissionGraph.Application.Features.Memberships.MembershipHandlerHelpers;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class LeaveOrganizationHandler(
    IValidator<LeaveOrganizationCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task HandleAsync(LeaveOrganizationCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireVisibleActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var membership = await GetTargetMembershipIncludingRemovedAsync(membershipRepository, organization.Id, actor.UserId, cancellationToken);

        if (!membership.IsActive)
        {
            throw new ConflictApplicationException("active_membership_required", "Active membership is required.");
        }

        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            membership.Remove(organization.OwnerUserId == actor.UserId, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization_member.left", "OrganizationMembership", membership.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }
}
