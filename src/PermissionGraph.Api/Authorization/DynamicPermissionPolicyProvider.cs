namespace PermissionGraph.Api.Authorization;

internal sealed class DynamicPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!PermissionPolicyNames.TryGetPermissionKey(policyName, out var permissionKey))
        {
            return await base.GetPolicyAsync(policyName);
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionKey))
            .Build();
    }
}
