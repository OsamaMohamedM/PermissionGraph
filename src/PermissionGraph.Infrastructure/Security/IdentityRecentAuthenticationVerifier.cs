using Microsoft.AspNetCore.Identity;
using PermissionGraph.Application.Abstractions.Security;
using PermissionGraph.Infrastructure.Authentication;

namespace PermissionGraph.Infrastructure.Security;

internal sealed class IdentityRecentAuthenticationVerifier(UserManager<ApplicationUser> userManager) : IRecentAuthenticationVerifier
{
    public async Task<bool> HasRecentAuthenticationAsync(Guid userId, string currentPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.IsActive && await userManager.CheckPasswordAsync(user, currentPassword);
    }
}
