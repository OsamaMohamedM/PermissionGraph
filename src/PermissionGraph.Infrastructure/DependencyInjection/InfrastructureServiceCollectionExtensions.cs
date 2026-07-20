using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Authentication;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Clock;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Abstractions.Email;
using PermissionGraph.Application.Abstractions.Identifiers;
using PermissionGraph.Application.Abstractions.Memberships;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Abstractions.Projects;
using PermissionGraph.Application.Abstractions.Security;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Authentication;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.Audit;
using PermissionGraph.Infrastructure.AuthorizationSeed;
using PermissionGraph.Infrastructure.Configuration;
using PermissionGraph.Infrastructure.Data;
using PermissionGraph.Infrastructure.Email;
using PermissionGraph.Infrastructure.Identifiers;
using PermissionGraph.Infrastructure.Memberships;
using PermissionGraph.Infrastructure.Organizations;
using PermissionGraph.Infrastructure.Projects;
using PermissionGraph.Infrastructure.Security;
using PermissionGraph.Infrastructure.Time;
using PermissionGraph.Infrastructure.Users;
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
        services.AddSingleton<IGuidProvider, GuidProvider>();
        services.AddDataProtection();

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
        services.AddScoped<IdentityAuthenticationService>();
        services.AddScoped<IAuthenticationService>(serviceProvider =>
            new ValidatingAuthenticationService(
                serviceProvider.GetRequiredService<IdentityAuthenticationService>(),
                serviceProvider));
        services.AddSingleton<IEmailDelivery, DevelopmentEmailDelivery>();
        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<IOrganizationMembershipRepository, EfOrganizationMembershipRepository>();
        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<IProjectAdministratorAssignmentService, EfProjectAdministratorAssignmentService>();
        services.AddScoped<IUserAccountLookup, IdentityUserAccountLookup>();
        services.AddScoped<IRecentAuthenticationVerifier, IdentityRecentAuthenticationVerifier>();
        services.AddScoped<IApplicationTransaction, EfApplicationTransaction>();
        services.AddScoped<IAuditWriter, EfAuditWriter>();
        services.AddScoped<IOrganizationSeedService, M02OrganizationSeedService>();

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
