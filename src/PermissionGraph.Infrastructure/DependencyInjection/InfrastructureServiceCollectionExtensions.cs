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
        services.AddScoped<IPermissionDefinitionRepository, EfPermissionDefinitionRepository>();
        services.AddScoped<IOrganizationPolicyVersionUpdater, EfOrganizationPolicyVersionUpdater>();
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
            ? PostgreSqlConnectionStringHelper.FromConfiguration(configuration)
            : configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Missing required connection string '{name}'.");
        }

        return connectionString;
    }
}