namespace PermissionGraph.Application.Abstractions.Services.Organizations;

public interface IOrganizationSeedService
{
    Task SeedDefaultAuthorizationAsync(Organization organization, Guid actorUserId, CancellationToken cancellationToken);
}