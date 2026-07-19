using Microsoft.Extensions.Diagnostics.HealthChecks;
using PermissionGraph.Infrastructure.DependencyInjection;

namespace PermissionGraph.Api.Configuration;

public static class ApiHealthCheckExtensions
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseConnection = InfrastructureServiceCollectionExtensions.RequireConnectionString(
            configuration,
            "PermissionGraph");
        var redisConnection = InfrastructureServiceCollectionExtensions.RequireConnectionString(
            configuration,
            "Redis");

        services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddNpgSql(databaseConnection, name: "postgresql", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
            .AddRedis(redisConnection, name: "redis", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

        return services;
    }
}
