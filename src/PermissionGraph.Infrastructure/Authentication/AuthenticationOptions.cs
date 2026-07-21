namespace PermissionGraph.Infrastructure.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public required string JwtSigningKey { get; init; }

    public required string JwtIssuer { get; init; }

    public required string JwtAudience { get; init; }

    public int JwtAccessTokenMinutes { get; init; } = 15;

    public required string RefreshTokenHashKey { get; init; }

    public int RefreshTokenDays { get; init; } = 30;

    public bool RequireConfirmedEmail { get; init; }

    public bool AutoConfirmEmail { get; init; }

    public bool NewUsersAreActive { get; init; } = true;

    public static AuthenticationOptions FromConfiguration(IConfiguration configuration)
    {
        return new AuthenticationOptions
        {
            JwtSigningKey = Read(configuration, "Authentication:JwtSigningKey", "AUTH_JWT_SIGNING_KEY"),
            JwtIssuer = Read(configuration, "Authentication:JwtIssuer", "AUTH_JWT_ISSUER"),
            JwtAudience = Read(configuration, "Authentication:JwtAudience", "AUTH_JWT_AUDIENCE"),
            JwtAccessTokenMinutes = ReadInt(configuration, "Authentication:JwtAccessTokenMinutes", "AUTH_JWT_ACCESS_TOKEN_MINUTES", 15),
            RefreshTokenHashKey = Read(configuration, "Authentication:RefreshTokenHashKey", "AUTH_REFRESH_TOKEN_HASH_KEY"),
            RefreshTokenDays = ReadInt(configuration, "Authentication:RefreshTokenDays", "AUTH_REFRESH_TOKEN_DAYS", 30),
            RequireConfirmedEmail = ReadBool(configuration, "Authentication:RequireConfirmedEmail", "AUTH_REQUIRE_CONFIRMED_EMAIL", false),
            AutoConfirmEmail = ReadBool(configuration, "Authentication:AutoConfirmEmail", "AUTH_AUTO_CONFIRM_EMAIL", false),
            NewUsersAreActive = ReadBool(configuration, "Authentication:NewUsersAreActive", "AUTH_NEW_USERS_ARE_ACTIVE", true)
        };
    }

    private static string Read(IConfiguration configuration, string key, string environmentKey)
    {
        return configuration[key] ?? Environment.GetEnvironmentVariable(environmentKey) ?? string.Empty;
    }

    private static int ReadInt(IConfiguration configuration, string key, string environmentKey, int defaultValue)
    {
        var value = Read(configuration, key, environmentKey);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool ReadBool(IConfiguration configuration, string key, string environmentKey, bool defaultValue)
    {
        var value = Read(configuration, key, environmentKey);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}