namespace PermissionGraph.Api.Configuration;

public static class StartupValidation
{
    public static void ValidateFoundationConfiguration(IConfiguration configuration)
    {
        InfrastructureServiceCollectionExtensions.RequireConnectionString(configuration, "PermissionGraph");
        InfrastructureServiceCollectionExtensions.RequireConnectionString(configuration, "Redis");
        ValidateAuthenticationConfiguration(configuration);
    }

    private static void ValidateAuthenticationConfiguration(IConfiguration configuration)
    {
        var options = AuthenticationOptions.FromConfiguration(configuration);

        if (options.JwtSigningKey.Length < 32)
        {
            throw new InvalidOperationException("Authentication JWT signing key must be at least 32 characters.");
        }

        if (options.RefreshTokenHashKey.Length < 32)
        {
            throw new InvalidOperationException("Authentication refresh-token hash key must be at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtIssuer))
        {
            throw new InvalidOperationException("Authentication JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtAudience))
        {
            throw new InvalidOperationException("Authentication JWT audience is required.");
        }

        if (options.JwtAccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("Authentication JWT access-token lifetime must be between 1 and 60 minutes.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException("Authentication refresh-token lifetime must be between 1 and 90 days.");
        }
    }
}