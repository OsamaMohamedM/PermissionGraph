namespace PermissionGraph.Api.Authorization;

internal static class EndpointPermissionConventionExtensions
{
    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        string permissionKey)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(PermissionPolicyNames.For(permissionKey));
    }
}
