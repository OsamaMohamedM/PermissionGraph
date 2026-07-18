using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Authentication;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Email;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.Configuration;
using PermissionGraph.Infrastructure.Data;
using PermissionGraph.Infrastructure.Email;
using PermissionGraph.Infrastructure.Time;
using StackExchange.Redis;

namespace PermissionGraph.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionGraphInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseConnection = RequireConnectionString(configuration, "PermissionGraph");
        var redisConnection = RequireConnectionString(configuration, "Redis");
        var authenticationOptions = AuthenticationOptions.FromConfiguration(configuration);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(authenticationOptions);

        services.AddDbContext<PermissionGraphDbContext>(options =>
            options.UseNpgsql(
                databaseConnection,
                npgsql => npgsql.MigrationsAssembly(typeof(PermissionGraphDbContext).Assembly.FullName)));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = authenticationOptions.RequireConfirmedEmail;
            })
            .AddEntityFrameworkStores<PermissionGraphDbContext>()
            .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(TokenOptions.DefaultProvider);

        services.AddScoped<JwtTokenIssuer>();
        services.AddScoped<RefreshTokenHasher>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddSingleton<IEmailDelivery, DevelopmentEmailDelivery>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        return services;
    }

    public static string RequireConnectionString(IConfiguration configuration, string name)
    {
        var connectionString = name == "PermissionGraph"
            ? PostgreSqlConnectionString.FromConfiguration(configuration)
            : configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Missing required connection string '{name}'.");
        }

        return connectionString;
    }
}
