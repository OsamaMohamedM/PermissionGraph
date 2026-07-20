namespace PermissionGraph.Application.Abstractions.Users;

public interface IUserAccountLookup
{
    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

public sealed record UserAccount(Guid UserId, string Email, string DisplayName, bool IsActive);
