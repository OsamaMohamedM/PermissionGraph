namespace PermissionGraph.IntegrationTests;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task Live_DoesNotRequirePostgreSqlOrRedis()
    {
        using var factory = CreateFactory(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            "127.0.0.1:1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Ready_IsHealthyWhenDependenciesAreAvailable()
    {
        await StartPostgreSqlAsync();
        await StartRedisAsync();

        using var factory = CreateFactory(_postgres!.GetConnectionString(), _redis!.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_IsUnhealthyWhenPostgreSqlIsUnavailable()
    {
        await StartRedisAsync();

        using var factory = CreateFactory(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing",
            _redis!.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().NotContain("password");
        body.ToLowerInvariant().Should().NotContain("username");
    }

    [Fact]
    public async Task Ready_IsDegradedWithOkStatusWhenRedisIsUnavailable()
    {
        await StartPostgreSqlAsync();

        using var factory = CreateFactory(_postgres!.GetConnectionString(), "127.0.0.1:1,connectTimeout=250");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("Degraded");
    }

    private static PermissionGraphApiFactory CreateFactory(string database, string redis)
    {
        return new PermissionGraphApiFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PermissionGraph"] = database,
            ["ConnectionStrings:Redis"] = redis
        });
    }

    private async Task StartPostgreSqlAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16.4-alpine").Build();
        await _postgres.StartAsync();
    }

    private async Task StartRedisAsync()
    {
        _redis = new RedisBuilder("redis:7.4.0-alpine").Build();
        await _redis.StartAsync();
    }
}