namespace PermissionGraph.Application.Abstractions.Organizations;

public interface IOrganizationPolicyVersionUpdater
{
    Task IncrementPolicyVersionAsync(
        Guid organizationId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
