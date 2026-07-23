namespace PermissionGraph.Api.Authorization;

internal static class PermissionPolicyNames
{
    public const string Prefix = "Permission:";

    public static string For(string permissionKey)
    {
        return $"{Prefix}{permissionKey}";
    }

    public static bool TryGetPermissionKey(string policyName, out string permissionKey)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            permissionKey = policyName[Prefix.Length..];
            return !string.IsNullOrWhiteSpace(permissionKey);
        }

        permissionKey = string.Empty;
        return false;
    }
}
