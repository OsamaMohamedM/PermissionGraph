using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PermissionGraph.IntegrationTests;

internal sealed class PermissionGraphApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;
    private readonly bool _clearConfiguration;
    private readonly Dictionary<string, string?> _previousEnvironment = [];

    public PermissionGraphApiFactory(Dictionary<string, string?> settings, bool clearConfiguration = false)
    {
        _settings = new Dictionary<string, string?>(settings)
        {
            ["Authentication:JwtSigningKey"] = "testing-jwt-signing-key-32-characters-minimum",
            ["Authentication:JwtIssuer"] = "PermissionGraph.Tests",
            ["Authentication:JwtAudience"] = "PermissionGraph.Tests",
            ["Authentication:JwtAccessTokenMinutes"] = "15",
            ["Authentication:RefreshTokenHashKey"] = "testing-refresh-hash-key-32-characters-minimum",
            ["Authentication:RefreshTokenDays"] = "30",
            ["Authentication:RequireConfirmedEmail"] = "false",
            ["Authentication:AutoConfirmEmail"] = "true",
            ["Authentication:NewUsersAreActive"] = "true"
        };
        _clearConfiguration = clearConfiguration;

        SetEnvironmentOverride("ConnectionStrings__PermissionGraph", "ConnectionStrings:PermissionGraph");
        SetEnvironmentOverride("ConnectionStrings__Redis", "ConnectionStrings:Redis");
        SetEnvironmentOverride("AUTH_JWT_SIGNING_KEY", "Authentication:JwtSigningKey");
        SetEnvironmentOverride("AUTH_JWT_ISSUER", "Authentication:JwtIssuer");
        SetEnvironmentOverride("AUTH_JWT_AUDIENCE", "Authentication:JwtAudience");
        SetEnvironmentOverride("AUTH_JWT_ACCESS_TOKEN_MINUTES", "Authentication:JwtAccessTokenMinutes");
        SetEnvironmentOverride("AUTH_REFRESH_TOKEN_HASH_KEY", "Authentication:RefreshTokenHashKey");
        SetEnvironmentOverride("AUTH_REFRESH_TOKEN_DAYS", "Authentication:RefreshTokenDays");
        SetEnvironmentOverride("AUTH_REQUIRE_CONFIRMED_EMAIL", "Authentication:RequireConfirmedEmail");
        SetEnvironmentOverride("AUTH_AUTO_CONFIRM_EMAIL", "Authentication:AutoConfirmEmail");
        SetEnvironmentOverride("AUTH_NEW_USERS_ARE_ACTIVE", "Authentication:NewUsersAreActive");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            if (_clearConfiguration)
            {
                configuration.Sources.Clear();
            }

            configuration.AddInMemoryCollection(_settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var item in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(item.Key, item.Value);
        }

        base.Dispose(disposing);
    }

    private void SetEnvironmentOverride(string environmentKey, string configurationKey)
    {
        _previousEnvironment[environmentKey] = Environment.GetEnvironmentVariable(environmentKey);

        if (_settings.TryGetValue(configurationKey, out var value))
        {
            Environment.SetEnvironmentVariable(environmentKey, value);
        }
    }
}
