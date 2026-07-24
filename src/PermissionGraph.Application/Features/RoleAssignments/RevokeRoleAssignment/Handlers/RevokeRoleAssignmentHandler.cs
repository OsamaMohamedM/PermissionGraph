namespace PermissionGraph.Application.Features.RoleAssignments.RevokeRoleAssignment.Handlers;

public sealed class RevokeRoleAssignmentHandler(
    IValidator<RevokeRoleAssignmentCommand> validator,
    AuthenticatedUserResolver authenticatedUserResolver,
    IOrganizationRepository organizationRepository,
    IRoleAssignmentRepository assignmentRepository,
    IAuthorizationDecisionService authorizationDecisionService,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction,
    IClock clock)
{
    public async Task<RoleAssignmentResult> HandleAsync(
        RevokeRoleAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var actor = await authenticatedUserResolver.RequireActiveUserAsync(cancellationToken);
        var organization = await RequireActiveOrganizationAsync(command.OrganizationId, cancellationToken);
        var assignment = await assignmentRepository.GetByOrganizationAndIdForMutationAsync(
            command.OrganizationId,
            command.AssignmentId,
            cancellationToken);

        if (assignment is null)
        {
            throw NotFound();
        }

        var actorIsOwner = organization.OwnerUserId == actor.UserId;
        if (!actorIsOwner)
        {
            await RequireActiveActorMembershipAsync(command.OrganizationId, actor.UserId, cancellationToken);

            if (assignment.UserId == actor.UserId)
            {
                throw new ForbiddenApplicationException(
                    "role_assignment_self_revoke_denied",
                    "A non-owner cannot revoke or modify their own assignment.");
            }

            await RequireAssignPermissionAsync(actor.UserId, assignment, cancellationToken);
        }

        var now = clock.UtcNow;
        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        try
        {
            assignment.Revoke(actor.UserId, command.Reason, now);
        }
        catch (DomainRuleViolationException exception)
        {
            throw DomainRuleViolationMapper.ToConflict(exception);
        }

        await membershipRepository.IncrementAuthorizationVersionAsync(
            assignment.OrganizationId,
            assignment.UserId,
            now,
            cancellationToken);
        await auditWriter.WriteAsync(
            new AuditRecord(assignment.OrganizationId, actor.UserId, "role_assignment.revoked", "RoleAssignment", assignment.Id, "Succeeded", now),
            cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return RoleAssignmentResult.FromDomain(assignment);
    }

    private async Task<Organization> RequireActiveOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        return organization is not null && organization.IsActive
            ? organization
            : throw OrganizationAccessHelper.NotFound();
    }

    private async Task RequireActiveActorMembershipAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetByOrganizationAndUserAsync(
            organizationId,
            actorUserId,
            cancellationToken);

        if (membership is null || !membership.IsActive)
        {
            throw OrganizationAccessHelper.NotFound();
        }
    }

    private async Task RequireAssignPermissionAsync(
        Guid actorUserId,
        RoleAssignment assignment,
        CancellationToken cancellationToken)
    {
        var projectId = assignment.ScopeType == RoleAssignmentScopeType.Project
            ? assignment.ScopeId
            : (Guid?)null;
        var decision = await authorizationDecisionService.CheckAsync(
            new CheckPermissionQuery(actorUserId, assignment.OrganizationId, projectId, "pg.roles.assign"),
            cancellationToken);

        if (!decision.Allowed)
        {
            throw new ForbiddenApplicationException(
                "role_assignment_not_authorized",
                "Actor is not allowed to revoke roles in this scope.");
        }
    }

    internal static NotFoundApplicationException NotFound()
    {
        return new NotFoundApplicationException(
            "role_assignment_not_found",
            "Role assignment could not be found.");
    }
}
