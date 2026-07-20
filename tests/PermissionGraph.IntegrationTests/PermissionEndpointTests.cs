using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Contracts.Authentication;
using PermissionGraph.Contracts.OrganizationMembers;
using PermissionGraph.Contracts.Organizations;
using PermissionGraph.Contracts.Permissions;
using PermissionGraph.Domain.Permissions;
using PermissionGraph.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace PermissionGraph.IntegrationTests;

public sealed class PermissionEndpointTests : IAsyncLifetime
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
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }
    }

    [Fact]
    public async Task PermissionEndpoints_ReturnUnauthorizedAnonymously()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/v1/organizations/{organizationId}/permissions"),
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/permissions", CreateRequest("billing.invoice.view")),
            await client.GetAsync($"/api/v1/organizations/{organizationId}/permissions/{permissionId}"),
            await client.PatchAsJsonAsync($"/api/v1/organizations/{organizationId}/permissions/{permissionId}", UpdateRequest()),
            await client.PostAsync($"/api/v1/organizations/{organizationId}/permissions/{permissionId}/archive", null),
            await client.PostAsync($"/api/v1/organizations/{organizationId}/permissions/{permissionId}/activate", null)
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task OwnerCreatesCustomPermissionWithValidLocation()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-create@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Create Api Org");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            CreateRequest("billing.invoice.view"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location?.ToString().Should().StartWith($"/api/v1/organizations/{organization.Id}/permissions/");
        var permission = (await response.Content.ReadFromJsonAsync<PermissionResponse>())!;
        permission.OrganizationId.Should().Be(organization.Id);
        permission.Key.Should().Be("billing.invoice.view");
        permission.PermissionType.Should().Be("Custom");

        var get = await client.GetAsync(response.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadFromJsonAsync<PermissionResponse>())!.Id.Should().Be(permission.Id);
    }

    [Fact]
    public async Task ActiveMemberListsAndGetsPlatformAndRouteOrganizationCustomPermissionsOnly()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "permission-owner-visible@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "permission-member-visible@example.test");

        await AuthorizeAsync(client, owner.Email);
        var first = await CreateOrganizationAsync(client, "Permission Visible First Org");
        var second = await CreateOrganizationAsync(client, "Permission Visible Second Org");
        var firstCustom = await CreatePermissionAsync(client, first.Id, "billing.invoice.view");
        var secondCustom = await CreatePermissionAsync(client, second.Id, "billing.invoice.view");
        await AddMemberAsync(client, first.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        var list = await client.GetAsync($"/api/v1/organizations/{first.Id}/permissions?pageSize=100");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await list.Content.ReadFromJsonAsync<PermissionListResponse>())!;
        body.Items.Should().Contain(item => item.PermissionType == "Platform" && item.Key == "pg.permissions.view");
        body.Items.Should().Contain(item => item.Id == firstCustom.Id);
        body.Items.Should().NotContain(item => item.Id == secondCustom.Id);

        var platformId = await PlatformPermissionIdAsync(factory, "pg.permissions.view");
        (await client.GetAsync($"/api/v1/organizations/{first.Id}/permissions/{platformId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/organizations/{first.Id}/permissions/{firstCustom.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var crossTenant = await client.GetAsync($"/api/v1/organizations/{first.Id}/permissions/{secondCustom.Id}");
        crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuspendedOrRemovedMembersCannotListOrGetPermissions()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "permission-owner-member-states@example.test");
        var suspendedMember = await RegisterAndAuthorizeAsync(client, "permission-suspended-member@example.test");
        var removedMember = await RegisterAndAuthorizeAsync(client, "permission-removed-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Permission Member State Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");
        await AddMemberAsync(client, organization.Id, suspendedMember.Email);
        await AddMemberAsync(client, organization.Id, removedMember.Email);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{suspendedMember.UserId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/organizations/{organization.Id}/members/{removedMember.UserId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AuthorizeAsync(client, suspendedMember.Email);
        (await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthorizeAsync(client, removedMember.Email);
        (await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestValidationAndDuplicateKeyUseProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-validation@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Validation Org");

        var reserved = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            CreateRequest("pg.custom.view"));
        reserved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(reserved, "Request validation failed.");

        var invalid = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            CreateRequest("Invalid Key"));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(invalid, "Request validation failed.");

        await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");
        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            CreateRequest("billing.invoice.view"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(duplicate, "A custom permission with this key already exists.");
    }

    [Fact]
    public async Task SameCustomKeyAllowedInDifferentOrganizations()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-cross-org-key@example.test");
        var first = await CreateOrganizationAsync(client, "Permission Key First Org");
        var second = await CreateOrganizationAsync(client, "Permission Key Second Org");

        var firstPermission = await CreatePermissionAsync(client, first.Id, "billing.invoice.view");
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{second.Id}/permissions",
            CreateRequest("billing.invoice.view"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<PermissionResponse>())!.Id.Should().NotBe(firstPermission.Id);
    }

    [Fact]
    public async Task NonOwnerCannotMutateAndTenantCannotMutatePlatformPermissions()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "permission-owner-forbidden@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "permission-member-forbidden@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Permission Forbidden Org");
        var custom = await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");
        var platformId = await PlatformPermissionIdAsync(factory, "pg.permissions.view");
        await AddMemberAsync(client, organization.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/permissions", CreateRequest("billing.invoice.approve"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}/permissions/{custom.Id}", UpdateRequest())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{custom.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{custom.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthorizeAsync(client, owner.Email);
        (await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}/permissions/{platformId}", UpdateRequest())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{platformId}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{platformId}/activate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateArchiveActivateLifecycleAndFilters()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-lifecycle@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Lifecycle Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");

        var update = await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}",
            UpdateRequest("Billing invoice read", "Updated.", "Billing", false));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<PermissionResponse>())!;
        updated.Key.Should().Be(permission.Key);
        updated.AllowedScopes.Should().Be(permission.AllowedScopes);
        updated.IsRequestable.Should().BeFalse();

        var archive = await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await archive.Content.ReadAsStringAsync()).Should().BeEmpty();
        var repeatedArchive = await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}/archive", null);
        repeatedArchive.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var archivedList = await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions?isActive=false&permissionType=Custom&module=Billing&allowedScope=Organization&search=billing.invoice");
        archivedList.StatusCode.Should().Be(HttpStatusCode.OK);
        (await archivedList.Content.ReadFromJsonAsync<PermissionListResponse>())!.Items.Should().ContainSingle(item => item.Id == permission.Id && !item.IsActive);

        var activate = await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}/activate", null);
        activate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await activate.Content.ReadAsStringAsync()).Should().BeEmpty();
        var repeatedActivate = await client.PostAsync($"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}/activate", null);
        repeatedActivate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ArchivedOrganizationBlocksPermissionMutation()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-archived-org@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Archived Org");

        (await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/archive",
            new ArchiveOrganizationRequest("ARCHIVE"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            CreateRequest("billing.invoice.view"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PaginationInvalidValuesAndProblemDetailsAreSafe()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-pagination@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Pagination Org");
        await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");

        var page = await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions?page=1&pageSize=1");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageBody = (await page.Content.ReadFromJsonAsync<PermissionListResponse>())!;
        pageBody.Page.Should().Be(1);
        pageBody.PageSize.Should().Be(1);

        var invalid = await client.GetAsync($"/api/v1/organizations/{organization.Id}/permissions?page=0&pageSize=101&permissionType=Nope&allowedScope=Bad");
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await AssertProblemAsync(invalid, "Request validation failed.");
        TryGetErrors(problem, out var errors).Should().BeTrue();
        errors.TryGetProperty("Page", out _).Should().BeTrue();
        errors.TryGetProperty("PageSize", out _).Should().BeTrue();

        var body = (await invalid.Content.ReadAsStringAsync()).ToUpperInvariant();
        body.Should().NotContain("NPGSQL");
        body.Should().NotContain("STACK");
        body.Should().NotContain("TOKEN");
        body.Should().NotContain("HASH");
        body.Should().NotContain("PASSWORD");
        body.Should().NotContain("SECRET");
        body.Should().NotContain("DATABASE");
    }

    [Fact]
    public async Task StaleConcurrencyUpdate_ReturnsConflictProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-concurrency@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Concurrency Api Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "billing.invoice.view");

        using var conflictFactory = await CreateMigratedFactoryAsync(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddScoped<IAuditWriter, ConcurrentPermissionUpdateAuditWriter>();
        });
        using var conflictClient = conflictFactory.CreateClient();
        conflictClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var response = await conflictClient.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions/{permission.Id}",
            UpdateRequest("Concurrency rename", null, "Billing", true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(response, "The resource was modified by another request.");
    }

    [Fact]
    public async Task PermissionMutationRateLimit_ReturnsProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "permission-owner-rate-limit@example.test");
        var organization = await CreateOrganizationAsync(client, "Permission Rate Limit Org");

        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/organizations/{organization.Id}/permissions",
                CreateRequest($"billing.invoice.rate_{attempt}"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }
        }

        limited.Should().NotBeNull();
        await AssertProblemAsync(limited!, "Too many requests.");
    }

    private async Task<PermissionGraphApiFactory> CreateMigratedFactoryAsync(Action<IServiceCollection>? configureServices = null)
    {
        var factory = new PermissionGraphApiFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PermissionGraph"] = _postgres!.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis!.GetConnectionString()
            },
            configureServices: configureServices);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        await dbContext.Database.MigrateAsync();

        return factory;
    }

    private static async Task<CurrentUserResponse> RegisterAndAuthorizeAsync(HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email[..email.IndexOf('@')], email, "ValidPassword123!", "ValidPassword123!"));
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        await AuthorizeAsync(client, email);
        return (await register.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
    }

    private static async Task AuthorizeAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "ValidPassword123!"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
    }

    private static async Task<OrganizationResponse> CreateOrganizationAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest(name, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<OrganizationResponse>())!;
    }

    private static async Task AddMemberAsync(HttpClient client, Guid organizationId, string email)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/members", new AddOrganizationMemberRequest(email));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<PermissionResponse> CreatePermissionAsync(HttpClient client, Guid organizationId, string key)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/permissions", CreateRequest(key));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PermissionResponse>())!;
    }

    private static CreateCustomPermissionRequest CreateRequest(string key)
    {
        return new CreateCustomPermissionRequest(
            key,
            "Billing invoice read",
            "Allows billing invoice read access.",
            "Billing",
            "Organization",
            true);
    }

    private static UpdateCustomPermissionRequest UpdateRequest(
        string displayName = "Billing invoice approve",
        string? description = "Allows billing invoice approval.",
        string module = "Billing",
        bool isRequestable = true)
    {
        return new UpdateCustomPermissionRequest(displayName, description, module, isRequestable);
    }

    private static async Task<Guid> PlatformPermissionIdAsync(PermissionGraphApiFactory factory, string key)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        return await dbContext.PermissionDefinitions
            .Where(permission => permission.OrganizationId == null && permission.Key == key)
            .Select(permission => permission.Id)
            .SingleAsync();
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, string title)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be(title);
        document.RootElement.GetProperty("status").GetInt32().Should().Be((int)response.StatusCode);
        document.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        return document;
    }

    private static bool TryGetErrors(JsonDocument problem, out JsonElement errors)
    {
        return problem.RootElement.TryGetProperty("errors", out errors)
            || problem.RootElement.TryGetProperty("Errors", out errors);
    }

    private sealed class ConcurrentPermissionUpdateAuditWriter(IServiceScopeFactory scopeFactory) : IAuditWriter
    {
        public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            if (record.Action != "permission.updated")
            {
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
            var permission = await dbContext.PermissionDefinitions.SingleAsync(item => item.Id == record.TargetId, cancellationToken);
            permission.UpdateMetadata("Concurrent permission rename", permission.Description, permission.Module, permission.IsRequestable, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
