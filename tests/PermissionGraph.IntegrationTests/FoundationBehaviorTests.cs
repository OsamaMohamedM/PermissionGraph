namespace PermissionGraph.IntegrationTests;

public sealed class FoundationBehaviorTests
{
    [Fact]
    public void StartupValidation_FailsWhenCriticalConfigurationIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PermissionGraph"] = string.Empty,
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();

        var act = () => StartupValidation.ValidateFoundationConfiguration(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PermissionGraph*");
    }

    [Fact]
    public void StartupValidation_FailsWhenRedisConfigurationIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PermissionGraph"] = "Host=127.0.0.1;Database=permissiongraph;Username=permissiongraph",
                ["ConnectionStrings:Redis"] = string.Empty
            })
            .Build();

        var act = () => StartupValidation.ValidateFoundationConfiguration(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Redis*");
    }

    [Fact]
    public async Task ProductionProblemDetails_HidesInternalExceptionDetails()
    {
        using var factory = new PermissionGraphApiFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PermissionGraph"] = "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["ConnectionStrings:Redis"] = "127.0.0.1:1"
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/__test/problem");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("traceId");
        body.Should().Contain("An unexpected error occurred.");
        body.Should().NotContain("Test exception details");
        body.Should().NotContain("InvalidOperationException");
        body.ToLowerInvariant().Should().NotContain("stack");
    }

    [Fact]
    public async Task SwaggerUi_IsAlwaysAvailableAndRootRedirectsToSwagger()
    {
        using var factory = new PermissionGraphApiFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PermissionGraph"] = "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            ["ConnectionStrings:Redis"] = "127.0.0.1:1"
        });
        using var redirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var client = factory.CreateClient();

        var root = await redirectClient.GetAsync("/");
        var ui = await client.GetAsync("/swagger/index.html");
        var document = await client.GetAsync("/swagger/v1/swagger.json");

        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location?.ToString().Should().Be("/swagger");
        ui.StatusCode.Should().Be(HttpStatusCode.OK);
        ui.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await ui.Content.ReadAsStringAsync()).Should().Contain("PermissionGraph API");
        document.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await JsonDocument.ParseAsync(await document.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("PermissionGraph API");
    }

    [Fact]
    public async Task EmptyMigration_AppliesToCleanPostgreSqlDatabase()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16.4-alpine")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PermissionGraphDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var dbContext = new PermissionGraphDbContext(options);
        await dbContext.Database.MigrateAsync();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().ContainSingle(migration => migration.EndsWith("_InitialEmpty", StringComparison.Ordinal));
    }
}
