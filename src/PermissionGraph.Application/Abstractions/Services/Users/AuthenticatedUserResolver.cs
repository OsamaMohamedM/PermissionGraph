using PermissionGraph.Application.Common.Errors;

namespace PermissionGraph.Application.Abstractions.Users;

public sealed class AuthenticatedUserResolver(ICurrentUser currentUser, IUserAccountLookup userAccountLookup)
{
    public async Task<UserAccount> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new UnauthorizedApplicationException("authentication_required", "Authentication is required.");
        }

        var account = await userAccountLookup.FindByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (account is null || !account.IsActive)
        {
            throw new UnauthorizedApplicationException("authentication_required", "Authentication is required.");
        }

        return account;
    }
}
