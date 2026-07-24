namespace PermissionGraph.Infrastructure.Services.RoleAssignments;

internal sealed class RoleAssignmentExpirationWorker(
    IServiceScopeFactory scopeFactory,
    RoleAssignmentExpirationOptions options,
    ILogger<RoleAssignmentExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ExpireBatchAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ExpireBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRoleAssignmentRepository>();
            var membershipRepository = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipRepository>();
            var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            var transaction = scope.ServiceProvider.GetRequiredService<IApplicationTransaction>();
            var now = clock.UtcNow;
            var assignments = await assignmentRepository.ListExpiredForUpdateAsync(
                now,
                options.BatchSize,
                cancellationToken);

            if (assignments.Count == 0)
            {
                return;
            }

            await using var tx = await transaction.BeginTransactionAsync(cancellationToken);
            foreach (var assignment in assignments)
            {
                var changed = assignment.Expire(now);
                if (!changed)
                {
                    continue;
                }

                await membershipRepository.IncrementAuthorizationVersionAsync(
                    assignment.OrganizationId,
                    assignment.UserId,
                    now,
                    cancellationToken);
                await auditWriter.WriteAsync(
                    new AuditRecord(assignment.OrganizationId, null, "role_assignment.expired", "RoleAssignment", assignment.Id, "Succeeded", now),
                    cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Role assignment expiration worker failed.");
        }
    }
}
