namespace PermissionGraph.Infrastructure.Services.Authentication;

internal sealed class RefreshTokenHasher(AuthenticationOptions options)
{
    public string Hash(string token)
    {
        var key = Encoding.UTF8.GetBytes(options.RefreshTokenHashKey);
        var bytes = Encoding.UTF8.GetBytes(token);

        return Convert.ToHexString(HMACSHA256.HashData(key, bytes));
    }

    public string HashUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return string.Empty;
        }

        var key = Encoding.UTF8.GetBytes(options.RefreshTokenHashKey);
        var bytes = Encoding.UTF8.GetBytes(userAgent);

        return Convert.ToHexString(HMACSHA256.HashData(key, bytes));
    }
}