using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Authentication;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using AuthResponse = PermissionGraph.Contracts.Authentication.AuthResponse;

namespace PermissionGraph.IntegrationTests;

public sealed class AuthenticationEndpointTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16.4-alpine").Build();
        _redis = new RedisBuilder("redis:7.4.0-alpine").Build();

        await _postgres.StartAsync();
        await _redis.StartAsync();
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
    public async Task RegisterLoginRefreshAndMe_UsesHashedRotatingRefreshTokens()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var register = await RegisterAsync(client, "alice@example.test");
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await LoginAsync(client, "alice@example.test");
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();
        loginBody.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var me = await client.GetAsync("/api/v1/users/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var storedSession = await FirstRefreshSessionAsync(factory);
        storedSession.TokenHash.Should().NotBe(loginBody.RefreshToken);
        storedSession.TokenHash.Should().HaveLength(64);
        storedSession.RotatedAtUtc.Should().BeNull();

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshCommand(loginBody.RefreshToken));
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        refreshBody.Should().NotBeNull();
        refreshBody!.RefreshToken.Should().NotBe(loginBody.RefreshToken);

        var sessions = await RefreshSessionsAsync(factory);
        sessions.Should().HaveCount(2);
        sessions.Should().Contain(session => session.RotatedAtUtc != null && session.ReplacedBySessionId != null);
        sessions.Should().Contain(session => session.RotatedAtUtc == null && session.RevokedAtUtc == null);
    }

    [Fact]
    public async Task ReusingRotatedRefreshToken_RevokesFullTokenFamily()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "reuse@example.test");
        var login = await LoginAsync(client, "reuse@example.test");
        var loginBody = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshCommand(loginBody.RefreshToken));
        var refreshBody = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshCommand(loginBody.RefreshToken));
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var familyRevoked = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshCommand(refreshBody.RefreshToken));
        familyRevoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var sessions = await RefreshSessionsAsync(factory);
        sessions.Should().OnlyContain(session => session.RevokedAtUtc != null);
    }

    [Fact]
    public async Task InactiveUser_CannotAuthenticate()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "inactive@example.test");
        await SetUserActiveAsync(factory, "inactive@example.test", isActive: false);

        var login = await LoginAsync(client, "inactive@example.test");

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwt_DoesNotContainDomainAuthorizationState()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "claims@example.test");
        var login = await LoginAsync(client, "claims@example.test");
        var loginBody = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(loginBody.AccessToken);
        var claimTypes = jwt.Claims.Select(claim => claim.Type).ToArray();

        claimTypes.Should().Contain("sub");
        claimTypes.Should().Contain("session_id");
        claimTypes.Should().Contain("security_stamp");
        claimTypes.Should().NotContain(["permission", "permissions", "role", "roles", "organization", "organizations", "membership", "memberships"]);
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAuthenticationForCurrentUser()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_RateLimit_IsIpBased()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        for (var index = 0; index < 3; index++)
        {
            var accepted = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordCommand("rate@example.test"));
            accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordCommand("rate@example.test"));
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ResetPassword_InvalidatesAccessAndRefreshTokens()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        await RegisterAsync(client, "reset@example.test");
        var login = await LoginAsync(client, "reset@example.test");
        var loginBody = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        var resetToken = await GeneratePasswordResetTokenAsync(factory, "reset@example.test");

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordCommand("reset@example.test", resetToken, "NewValid123!", "NewValid123!"));
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        var meWithOldAccessToken = await client.GetAsync("/api/v1/users/me");
        meWithOldAccessToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshWithOldRefreshToken = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshCommand(loginBody.RefreshToken));
        refreshWithOldRefreshToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<PermissionGraphApiFactory> CreateMigratedFactoryAsync()
    {
        var factory = new PermissionGraphApiFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PermissionGraph"] = _postgres!.GetConnectionString(),
            ["ConnectionStrings:Redis"] = _redis!.GetConnectionString()
        });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        await dbContext.Database.MigrateAsync();

        return factory;
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand("Test User", email, "ValidPassword123!", "ValidPassword123!"));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(email, "ValidPassword123!"));
    }

    private static async Task<RefreshSession> FirstRefreshSessionAsync(PermissionGraphApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        return await dbContext.RefreshSessions.AsNoTracking().SingleAsync();
    }

    private static async Task<List<RefreshSession>> RefreshSessionsAsync(PermissionGraphApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        return await dbContext.RefreshSessions.AsNoTracking().OrderBy(session => session.CreatedAtUtc).ToListAsync();
    }

    private static async Task SetUserActiveAsync(PermissionGraphApiFactory factory, string email, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Email == email);
        user.IsActive = isActive;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> GeneratePasswordResetTokenAsync(PermissionGraphApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }
}
