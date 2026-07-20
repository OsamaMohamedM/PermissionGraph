namespace PermissionGraph.Application.Abstractions.Security;

public interface IRecentAuthenticationVerifier
{
    Task<bool> HasRecentAuthenticationAsync(Guid userId, string currentPassword, CancellationToken cancellationToken);
}
