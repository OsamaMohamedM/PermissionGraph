using FluentValidation;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Identifiers;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.Common.Pagination;
using PermissionGraph.Application.Common.Validation;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Common;
using PermissionGraph.Domain.Memberships;
using static PermissionGraph.Application.Features.Memberships.MembershipHandlerHelpers;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class AddOrganizationMemberHandler(
    IValidator<AddOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
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

public sealed class GetOrganizationMemberHandler(
    IValidator<GetOrganizationMemberQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<OrganizationMemberResult> HandleAsync(GetOrganizationMemberQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        var result = await membershipRepository.GetMemberResultAsync(query.OrganizationId, query.UserId, cancellationToken);
        if (result is null || result.Status == MembershipStatus.Removed)
        {
            throw OrganizationAccess.NotFound();
        }

        return result;
    }
}

public sealed class ListOrganizationMembersHandler(
    IValidator<ListOrganizationMembersQuery> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository)
{
    public async Task<PagedResult<OrganizationMemberResult>> HandleAsync(ListOrganizationMembersQuery query, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, query, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        await organizationAccess.RequireVisibleActiveOrganizationAsync(query.OrganizationId, actor.UserId, cancellationToken);

        return await membershipRepository.ListMembersAsync(
            query.OrganizationId,
            query.PageSize,
            query.Cursor,
            query.Search,
            query.Status,
            cancellationToken);
    }
}

public sealed class SuspendOrganizationMemberHandler(
    IValidator<SuspendOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
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

public sealed class ReactivateOrganizationMemberHandler(
    IValidator<ReactivateOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<OrganizationMemberResult> HandleAsync(ReactivateOrganizationMemberCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var membership = await GetTargetMembershipIncludingRemovedAsync(membershipRepository, organization.Id, command.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            membership.Reactivate(now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization_member.reactivated", "OrganizationMembership", membership.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationMemberResult.FromDomain(membership);
    }
}

public sealed class RemoveOrganizationMemberHandler(
    IValidator<RemoveOrganizationMemberCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    OrganizationAccess organizationAccess,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<OrganizationMemberResult> HandleAsync(RemoveOrganizationMemberCommand command, CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await organizationAccess.RequireOwnerActiveOrganizationAsync(command.OrganizationId, actor.UserId, cancellationToken);
        var membership = await GetTargetMembershipIncludingRemovedAsync(membershipRepository, organization.Id, command.UserId, cancellationToken);
        var now = clock.UtcNow;

        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            membership.Remove(organization.OwnerUserId == command.UserId, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await auditWriter.WriteAsync(
            new AuditRecord(organization.Id, actor.UserId, "organization_member.removed", "OrganizationMembership", membership.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return OrganizationMemberResult.FromDomain(membership);
    }
}

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

internal static class MembershipHandlerHelpers
{
    public static async Task<OrganizationMembership> GetTargetMembershipAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccess.NotFound();
    }

    public static async Task<OrganizationMembership> GetTargetMembershipIncludingRemovedAsync(
        IOrganizationMembershipRepository membershipRepository,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserIncludingRemovedAsync(organizationId, userId, cancellationToken);
        return membership ?? throw OrganizationAccess.NotFound();
    }
}
