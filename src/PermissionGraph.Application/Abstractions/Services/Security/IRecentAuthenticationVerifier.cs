namespace PermissionGraph.Application.Abstractions.Services.Security;

public interface IRecentAuthenticationVerifier
{
    Task<bool> HasRecentAuthenticationAsync(Guid userId, string currentPassword, CancellationToken cancellationToken);
}