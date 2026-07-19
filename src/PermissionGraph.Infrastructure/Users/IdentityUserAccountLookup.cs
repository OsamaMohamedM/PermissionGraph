using Microsoft.AspNetCore.Identity;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Infrastructure.Authentication;

namespace PermissionGraph.Infrastructure.Users;

internal sealed class IdentityUserAccountLookup(UserManager<ApplicationUser> userManager) : IUserAccountLookup
{
    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return ToAccount(user);
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        return ToAccount(user);
    }

    private static UserAccount? ToAccount(ApplicationUser? user)
    {
        if (user is null)
        {
            return null;
        }

        return new UserAccount(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive);
    }
}
