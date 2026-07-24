namespace PermissionGraph.Application.Features.RoleAssignments.ExpireRoleAssignments.Handlers;

public sealed class ExpireRoleAssignmentsHandler(
    IValidator<ExpireRoleAssignmentsCommand> validator,
    IRoleAssignmentRepository assignmentRepository,
    IOrganizationMembershipRepository membershipRepository,
    IAuditWriter auditWriter,
    IApplicationTransaction transaction)
{
    public async Task<ExpireRoleAssignmentsResult> HandleAsync(
        ExpireRoleAssignmentsCommand command,
        CancellationToken cancellationToken)
    {
        await ValidationRunner.ValidateAsync(validator, command, cancellationToken);
        var assignments = await assignmentRepository.ListExpiredForUpdateAsync(
            command.NowUtc,
            command.BatchSize,
            cancellationToken);

        var expiredCount = 0;
        await using var scope = await transaction.BeginTransactionAsync(cancellationToken);
        foreach (var assignment in assignments)
        {
            var changed = assignment.Expire(command.NowUtc);
            if (!changed)
            {
                continue;
            }

            expiredCount++;
            await membershipRepository.IncrementAuthorizationVersionAsync(
                assignment.OrganizationId,
                assignment.UserId,
                command.NowUtc,
                cancellationToken);
            await auditWriter.WriteAsync(
                new AuditRecord(assignment.OrganizationId, null, "role_assignment.expired", "RoleAssignment", assignment.Id, "Succeeded", command.NowUtc),
                cancellationToken);
        }

        await scope.CommitAsync(cancellationToken);
        return new ExpireRoleAssignmentsResult(expiredCount);
    }
}
