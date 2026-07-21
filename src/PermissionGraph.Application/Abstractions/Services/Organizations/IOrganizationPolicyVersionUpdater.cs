namespace PermissionGraph.Application.Abstractions.Services.Organizations;

public interface IOrganizationPolicyVersionUpdater
{
    Task IncrementPolicyVersionAsync(
        Guid organizationId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}