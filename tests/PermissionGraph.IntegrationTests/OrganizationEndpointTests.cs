using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Data;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Contracts.Authentication;
using PermissionGraph.Contracts.OrganizationMembers;
using PermissionGraph.Contracts.Organizations;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace PermissionGraph.IntegrationTests;

public sealed class OrganizationEndpointTests : IAsyncLifetime
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
    public async Task AnonymousOrganizationRequests_ReturnUnauthorizedProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, "Authentication is required.");
    }

    [Fact]
    public async Task CreateListAndGetOrganization_ReturnSafeContractResponses()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-create-org@example.test");

        var create = await client.PostAsJsonAsync(
            "/api/v1/organizations",
            new CreateOrganizationRequest("Acme Platform", "Engineering"));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location?.ToString().Should().StartWith("/api/v1/organizations/");
        var created = (await create.Content.ReadFromJsonAsync<OrganizationResponse>())!;
        created.OwnerUserId.Should().Be(owner.UserId);
        created.Status.Should().Be(nameof(OrganizationStatus.Active));

        var list = await client.GetFromJsonAsync<OrganizationListResponse>("/api/v1/organizations?pageSize=10");
        list.Should().NotBeNull();
        list!.PageSize.Should().Be(10);
        list.Items.Should().ContainSingle(item => item.Id == created.Id);

        var get = await client.GetFromJsonAsync<OrganizationResponse>($"/api/v1/organizations/{created.Id}");
        get.Should().NotBeNull();
        get!.Id.Should().Be(created.Id);
        get.Name.Should().Be(created.Name);
        get.Description.Should().Be(created.Description);
        get.OwnerUserId.Should().Be(created.OwnerUserId);
        get.Status.Should().Be(created.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var ownerMembership = await dbContext.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.OrganizationId == created.Id && item.UserId == owner.UserId);
        ownerMembership.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task ListOrganizations_ReturnsOnlyVisibleOrganizationsAndStablePaginationMetadata()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "visible-owner@example.test");
        var visible = await CreateOrganizationAsync(client, "Visible Org");

        await RegisterAndAuthorizeAsync(client, "hidden-owner@example.test");
        var hidden = await CreateOrganizationAsync(client, "Hidden Org");

        await AuthorizeAsync(client, "visible-owner@example.test");
        var defaultPage = await client.GetFromJsonAsync<OrganizationListResponse>("/api/v1/organizations");
        defaultPage.Should().NotBeNull();
        defaultPage!.PageSize.Should().Be(20);
        defaultPage.Items.Should().ContainSingle(item => item.Id == visible.Id);
        defaultPage.Items.Should().NotContain(item => item.Id == hidden.Id);

        var maximumPage = await client.GetFromJsonAsync<OrganizationListResponse>("/api/v1/organizations?pageSize=100");
        maximumPage!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task CrossTenantOrganizationAccess_ReturnsSafeNotFound()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "owner-cross-tenant@example.test");
        var organization = await CreateOrganizationAsync(client, "Cross Tenant Org");

        await RegisterAndAuthorizeAsync(client, "outsider-cross-tenant@example.test");

        var response = await client.GetAsync($"/api/v1/organizations/{organization.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemAsync(response, "Organization could not be found.");
    }

    [Fact]
    public async Task OwnerCanUpdateAndArchiveButMemberMutationIsForbiddenAndArchivedBlocksMutations()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "owner-update@example.test");
        var organization = await CreateOrganizationAsync(client, "Mutable Org");
        var member = await RegisterAndAuthorizeAsync(client, "member-update@example.test");

        await AuthorizeAsync(client, "owner-update@example.test");
        var add = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(member.Email));
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        await AuthorizeAsync(client, member.Email);
        var forbidden = await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}", new UpdateOrganizationRequest("Member Rename", null));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(forbidden, "Only the organization owner may perform this operation.");

        await AuthorizeAsync(client, "owner-update@example.test");
        var update = await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}", new UpdateOrganizationRequest("Renamed Org", "Updated"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<OrganizationResponse>())!;
        updated.Name.Should().Be("Renamed Org");

        var archive = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/archive", new ArchiveOrganizationRequest("ARCHIVE"));
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNoContentBodyAsync(archive);

        var updateArchived = await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}", new UpdateOrganizationRequest("Archived Rename", null));
        updateArchived.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemAsync(updateArchived, "Organization could not be found.");

        var addToArchived = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(member.Email));
        addToArchived.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemAsync(addToArchived, "Organization could not be found.");
    }

    [Fact]
    public async Task MemberEndpoints_AddListGetSuspendReactivateRemoveAndAccessLoss()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-members@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "member-members@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Member Lifecycle Org");
        var add = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(member.Email));
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = (await add.Content.ReadFromJsonAsync<OrganizationMemberResponse>())!;
        added.UserId.Should().Be(member.UserId);
        added.Status.Should().Be(nameof(MembershipStatus.Active));

        var list = await client.GetFromJsonAsync<OrganizationMemberListResponse>($"/api/v1/organizations/{organization.Id}/members?pageSize=5&status=Active");
        list.Should().NotBeNull();
        list!.PageSize.Should().Be(5);
        list.Items.Should().Contain(item => item.UserId == owner.UserId);
        list.Items.Should().Contain(item => item.UserId == member.UserId);

        var get = await client.GetFromJsonAsync<OrganizationMemberResponse>($"/api/v1/organizations/{organization.Id}/members/{member.UserId}");
        get!.Email.Should().Be(member.Email);

        await AuthorizeAsync(client, member.Email);
        var visibleToMember = await client.GetAsync($"/api/v1/organizations/{organization.Id}");
        visibleToMember.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthorizeAsync(client, owner.Email);
        var suspend = await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{member.UserId}/suspend", null);
        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNoContentBodyAsync(suspend);

        await AuthorizeAsync(client, member.Email);
        var suspendedAccess = await client.GetAsync($"/api/v1/organizations/{organization.Id}");
        suspendedAccess.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthorizeAsync(client, owner.Email);
        var reactivate = await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{member.UserId}/reactivate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNoContentBodyAsync(reactivate);
        var remove = await client.DeleteAsync($"/api/v1/organizations/{organization.Id}/members/{member.UserId}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNoContentBodyAsync(remove);

        await AuthorizeAsync(client, member.Email);
        var removedAccess = await client.GetAsync($"/api/v1/organizations/{organization.Id}");
        removedAccess.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonOwnerActiveMemberCanLeaveAndThenLosesAccess()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-leave@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "member-leave@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Leave Org");
        (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(member.Email))).StatusCode.Should().Be(HttpStatusCode.Created);

        await AuthorizeAsync(client, member.Email);
        var leave = await client.PostAsync($"/api/v1/organizations/{organization.Id}/leave", null);
        leave.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNoContentBodyAsync(leave);

        var afterLeave = await client.GetAsync($"/api/v1/organizations/{organization.Id}");
        afterLeave.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OwnerProtectionAndDuplicateMembership_ReturnConflictProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-protection@example.test");
        var organization = await CreateOrganizationAsync(client, "Owner Protected Org");

        var duplicate = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(owner.Email));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(duplicate, "Organization membership already exists.");

        var suspendOwner = await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{owner.UserId}/suspend", null);
        suspendOwner.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(suspendOwner, "The organization owner cannot be suspended.");

        var removeOwner = await client.DeleteAsync($"/api/v1/organizations/{organization.Id}/members/{owner.UserId}");
        removeOwner.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(removeOwner, "The organization owner cannot be removed.");

        var leave = await client.PostAsync($"/api/v1/organizations/{organization.Id}/leave", null);
        leave.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(leave, "The organization owner cannot be removed.");
    }

    [Fact]
    public async Task TransferOwnership_RequiresValidCurrentPassword()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-transfer-api@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "member-transfer-api@example.test");
        var suspendedTarget = await RegisterAndAuthorizeAsync(client, "suspended-transfer-api@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Transfer Api Org");
        (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(member.Email))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(suspendedTarget.Email))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{suspendedTarget.UserId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var invalid = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/transfer-ownership",
            new TransferOwnershipRequest(member.UserId, "WrongPassword123!"));
        invalid.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var invalidBody = await invalid.Content.ReadAsStringAsync();
        invalidBody.Should().NotContain("WrongPassword123!");
        await AssertProblemAsync(invalid, "Recent authentication is required.");

        var inactiveTarget = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/transfer-ownership",
            new TransferOwnershipRequest(suspendedTarget.UserId, "ValidPassword123!"));
        inactiveTarget.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(inactiveTarget, "Target owner must be an active organization member.");

        var transfer = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/transfer-ownership",
            new TransferOwnershipRequest(member.UserId, "ValidPassword123!"));
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);
        var transferred = (await transfer.Content.ReadFromJsonAsync<OrganizationResponse>())!;
        transferred.OwnerUserId.Should().Be(member.UserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var memberships = await dbContext.OrganizationMemberships.AsNoTracking().Where(item => item.OrganizationId == organization.Id).ToListAsync();
        memberships.Single(item => item.UserId == owner.UserId).AuthorizationVersion.Should().Be(2);
        memberships.Single(item => item.UserId == member.UserId).AuthorizationVersion.Should().Be(2);
    }

    [Fact]
    public async Task ValidationAndCommandValidation_ReturnProblemDetailsBeforeUnsafeWork()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "validation-org@example.test");

        var invalidRequest = await client.PostAsJsonAsync("/api/v1/organizations", new CreateOrganizationRequest("", "description"));
        invalidRequest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var requestProblem = await AssertProblemAsync(invalidRequest, "Request validation failed.");
        TryGetErrors(requestProblem, out var requestErrors).Should().BeTrue();
        requestErrors.EnumerateObject().Should().Contain(property => property.Name == "Name");

        var invalidCommand = await client.GetAsync($"/api/v1/organizations/{Guid.Empty}");
        invalidCommand.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var commandProblem = await AssertProblemAsync(invalidCommand, "Request validation failed.");
        TryGetErrors(commandProblem, out var commandErrors).Should().BeTrue();
        commandErrors.EnumerateObject().Should().Contain(property => property.Name == "OrganizationId");

        var invalidPaging = await client.GetAsync("/api/v1/organizations?pageSize=101");
        invalidPaging.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(invalidPaging, "Request validation failed.");

        var invalidMemberPaging = await client.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}/members?pageSize=0");
        invalidMemberPaging.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(invalidMemberPaging, "Request validation failed.");
    }

    [Fact]
    public async Task ApplicationConflict_ReturnsSafeProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "conflict-api@example.test");
        var organization = await CreateOrganizationAsync(client, "Conflict Api Org");

        using var conflictFactory = await CreateMigratedFactoryAsync(services =>
        {
            services.AddScoped<IApplicationTransaction, ThrowingConflictTransaction>();
        });
        using var conflictClient = conflictFactory.CreateClient();
        conflictClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var response = await conflictClient.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}", new UpdateOrganizationRequest("Conflict Rename", null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(response, "The record was modified by another request.");
    }

    [Fact]
    public async Task SensitiveMutationRateLimit_ReturnsProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "owner-rate-limit@example.test");
        var target = await RegisterAndAuthorizeAsync(client, "target-rate-limit@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Rate Limited Org");
        (await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/members", new AddOrganizationMemberRequest(target.Email))).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            limited = await client.PostAsJsonAsync(
                $"/api/v1/organizations/{organization.Id}/transfer-ownership",
                new TransferOwnershipRequest(target.UserId, "WrongPassword123!"));
        }

        limited.Should().NotBeNull();
        limited!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await AssertProblemAsync(limited, "Too many requests.");
    }

    [Fact]
    public async Task UnexpectedErrors_ReturnSafeProblemDetailsWithoutSensitiveValues()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "safe-error@example.test");
        var organization = await CreateOrganizationAsync(client, "Safe Error Org");

        using var failingFactory = await CreateMigratedFactoryAsync(services =>
        {
            services.AddScoped<IApplicationTransaction, ThrowingUnexpectedTransaction>();
        });
        using var failingClient = failingFactory.CreateClient();
        failingClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var response = await failingClient.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}",
            new UpdateOrganizationRequest("Safe Error Rename", "currentPassword token hash database stack trace"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await AssertProblemAsync(response, "An unexpected error occurred.");
        var body = (await response.Content.ReadAsStringAsync()).ToUpperInvariant();
        body.Should().NotContain("CURRENTPASSWORD");
        body.Should().NotContain("TOKEN");
        body.Should().NotContain("HASH");
        body.Should().NotContain("DATABASE");
        body.Should().NotContain("STACK");
        body.Should().NotContain("SECRET");
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

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, string title)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be(title);
        document.RootElement.GetProperty("status").GetInt32().Should().Be((int)response.StatusCode);
        document.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        return document;
    }

    private static async Task AssertNoContentBodyAsync(HttpResponseMessage response)
    {
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    private static bool TryGetErrors(JsonDocument problem, out JsonElement errors)
    {
        return problem.RootElement.TryGetProperty("errors", out errors)
            || problem.RootElement.TryGetProperty("Errors", out errors);
    }

    private sealed class ThrowingConflictTransaction : IApplicationTransaction
    {
        public Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IApplicationTransactionScope>(new Scope());
        }

        private sealed class Scope : IApplicationTransactionScope
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                throw new ConflictApplicationException("concurrency_conflict", "The record was modified by another request.");
            }
        }
    }

    private sealed class ThrowingUnexpectedTransaction : IApplicationTransaction
    {
        public Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IApplicationTransactionScope>(new Scope());
        }

        private sealed class Scope : IApplicationTransactionScope
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("secret currentPassword token hash database stack trace");
            }
        }
    }
}
