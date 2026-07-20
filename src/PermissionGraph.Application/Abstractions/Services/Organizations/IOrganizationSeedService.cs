using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Abstractions.Organizations;

public interface IOrganizationSeedService
{
    Task SeedDefaultAuthorizationAsync(Organization organization, Guid actorUserId, CancellationToken cancellationToken);
}
