namespace PermissionGraph.IntegrationTests;

public sealed class RoleEndpointTests : IAsyncLifetime
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
    public async Task RoleEndpoints_ReturnUnauthorizedAnonymously()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/v1/organizations/{organizationId}/roles"),
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/roles", CreateRoleRequest("Editors", "Organization", [])),
            await client.GetAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}"),
            await client.PatchAsJsonAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}", UpdateRoleRequest()),
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}/clone", CloneRequest("Copied Editors")),
            await client.PostAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}/archive", null),
            await client.PostAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}/activate", null),
            await client.PutAsJsonAsync($"/api/v1/organizations/{organizationId}/roles/{roleId}/permissions", new ReplaceRolePermissionsRequest([]))
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task OwnerCreatesRoleWithValidLocationAndMemberCanListAndGet()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "role-owner-create@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "role-member-visible@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Role Create Api Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");
        await AddMemberAsync(client, organization.Id, member.Email);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles",
            CreateRoleRequest("Document Reviewers", "Organization", [permission.Id]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location?.ToString().Should().StartWith($"/api/v1/organizations/{organization.Id}/roles/");
        var role = (await response.Content.ReadFromJsonAsync<RoleResponse>())!;
        role.OrganizationId.Should().Be(organization.Id);
        role.Name.Should().Be("Document Reviewers");
        role.RoleType.Should().Be("Custom");
        role.ScopeType.Should().Be("Organization");
        role.PermissionIds.Should().ContainSingle(permissionId => permissionId == permission.Id);

        var get = await client.GetAsync(response.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthorizeAsync(client, member.Email);
        var list = await client.GetAsync($"/api/v1/organizations/{organization.Id}/roles?pageSize=100");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<RoleListResponse>())!.Items.Should().Contain(item => item.Id == role.Id);
        (await client.GetAsync($"/api/v1/organizations/{organization.Id}/roles/{role.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonMemberAndCrossTenantRoleAccess_ReturnSafeNotFound()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "role-owner-cross-tenant@example.test");
        var outsider = await RegisterAndAuthorizeAsync(client, "role-outsider-cross-tenant@example.test");

        await AuthorizeAsync(client, owner.Email);
        var first = await CreateOrganizationAsync(client, "Role First Tenant Org");
        var second = await CreateOrganizationAsync(client, "Role Second Tenant Org");
        var firstPermission = await CreatePermissionAsync(client, first.Id, "documents.review", "Organization");
        var secondPermission = await CreatePermissionAsync(client, second.Id, "documents.review", "Organization");
        var firstRole = await CreateRoleAsync(client, first.Id, "Document Reviewers", "Organization", [firstPermission.Id]);
        var secondRole = await CreateRoleAsync(client, second.Id, "Document Reviewers", "Organization", [secondPermission.Id]);

        await AuthorizeAsync(client, outsider.Email);
        (await client.GetAsync($"/api/v1/organizations/{first.Id}/roles")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthorizeAsync(client, owner.Email);
        (await client.GetAsync($"/api/v1/organizations/{first.Id}/roles/{secondRole.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PatchAsJsonAsync($"/api/v1/organizations/{first.Id}/roles/{secondRole.Id}", UpdateRoleRequest())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        firstRole.Id.Should().NotBe(secondRole.Id);
    }

    [Fact]
    public async Task NonOwnerMutationDuplicateNamesAndValidationUseProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "role-owner-validation@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "role-member-validation@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Role Validation Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");
        var role = await CreateRoleAsync(client, organization.Id, "Document Reviewers", "Organization", [permission.Id]);
        await AddMemberAsync(client, organization.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        (await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}/roles/{role.Id}", UpdateRoleRequest())).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthorizeAsync(client, owner.Email);
        var invalid = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles",
            CreateRoleRequest("ab", "BadScope", [permission.Id]));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(invalid, "Request validation failed.");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles",
            CreateRoleRequest("document reviewers", "Organization", [permission.Id]));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(duplicate, "An active role with this name already exists in this organization and scope.");
    }

    [Fact]
    public async Task CustomRoleLifecycleCloneFiltersAndPermissionReplacement()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "role-owner-lifecycle@example.test");
        var organization = await CreateOrganizationAsync(client, "Role Lifecycle Org");
        var organizationPermission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");
        var projectPermission = await CreatePermissionAsync(client, organization.Id, "documents.approve", "Project");
        var sharedPermission = await PlatformPermissionAsync(factory, "pg.roles.view");
        var role = await CreateRoleAsync(client, organization.Id, "Document Reviewers", "Organization", [organizationPermission.Id]);

        var update = await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles/{role.Id}",
            UpdateRoleRequest("Senior Reviewers", "Updated.", false));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<RoleResponse>())!.IsRequestable.Should().BeFalse();

        var clone = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles/{role.Id}/clone",
            CloneRequest("Copied Reviewers"));
        clone.StatusCode.Should().Be(HttpStatusCode.Created);
        var cloned = (await clone.Content.ReadFromJsonAsync<RoleResponse>())!;
        cloned.RoleType.Should().Be("Custom");
        cloned.Id.Should().NotBe(role.Id);

        var incompatible = await client.PutAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles/{role.Id}/permissions",
            new ReplaceRolePermissionsRequest([projectPermission.Id]));
        incompatible.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var replace = await client.PutAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles/{role.Id}/permissions",
            new ReplaceRolePermissionsRequest([sharedPermission.Id]));
        replace.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replace.Content.ReadFromJsonAsync<RoleResponse>())!.PermissionIds.Should().ContainSingle(permissionId => permissionId == sharedPermission.Id);

        var noOp = await client.PutAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/roles/{role.Id}/permissions",
            new ReplaceRolePermissionsRequest([sharedPermission.Id]));
        noOp.StatusCode.Should().Be(HttpStatusCode.OK);

        var archive = await client.PostAsync($"/api/v1/organizations/{organization.Id}/roles/{role.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var archivedList = await client.GetAsync($"/api/v1/organizations/{organization.Id}/roles?isActive=false&roleType=Custom&scopeType=Organization&search=senior&pageSize=100");
        archivedList.StatusCode.Should().Be(HttpStatusCode.OK);
        (await archivedList.Content.ReadFromJsonAsync<RoleListResponse>())!.Items.Should().ContainSingle(item => item.Id == role.Id && !item.IsActive);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/roles/{role.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SystemRoleMutationAndCrossTenantPermissionReplacementAreBlocked()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "role-owner-system@example.test");
        var first = await CreateOrganizationAsync(client, "Role System First Org");
        var second = await CreateOrganizationAsync(client, "Role System Second Org");
        var secondPermission = await CreatePermissionAsync(client, second.Id, "documents.review", "Organization");
        var systemRole = await SystemRoleAsync(factory, first.Id, "ORGANIZATION ADMINISTRATOR");

        (await client.PatchAsJsonAsync($"/api/v1/organizations/{first.Id}/roles/{systemRole.Id}", UpdateRoleRequest())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync($"/api/v1/organizations/{first.Id}/roles/{systemRole.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync($"/api/v1/organizations/{first.Id}/roles/{systemRole.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PutAsJsonAsync($"/api/v1/organizations/{first.Id}/roles/{systemRole.Id}/permissions", new ReplaceRolePermissionsRequest([]))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var firstPermission = await CreatePermissionAsync(client, first.Id, "documents.approve", "Organization");
        var custom = await CreateRoleAsync(client, first.Id, "Approvers", "Organization", [firstPermission.Id]);
        var crossTenant = await client.PutAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/roles/{custom.Id}/permissions",
            new ReplaceRolePermissionsRequest([secondPermission.Id]));
        crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RoleMutationRateLimit_ReturnsProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "role-owner-rate-limit@example.test");
        var organization = await CreateOrganizationAsync(client, "Role Rate Limit Org");
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");

        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/organizations/{organization.Id}/roles",
                CreateRoleRequest($"Rate Role {attempt}", "Organization", [permission.Id]));
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
        await scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>().Database.MigrateAsync();
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

    private static async Task<PermissionResponse> CreatePermissionAsync(HttpClient client, Guid organizationId, string key, string allowedScopes)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/permissions",
            new CreateCustomPermissionRequest(key, "Document permission", null, "Documents", allowedScopes, true));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PermissionResponse>())!;
    }

    private static async Task<RoleResponse> CreateRoleAsync(HttpClient client, Guid organizationId, string name, string scopeType, IReadOnlyCollection<Guid> permissionIds)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/roles", CreateRoleRequest(name, scopeType, permissionIds));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RoleResponse>())!;
    }

    private static CreateCustomRoleRequest CreateRoleRequest(string name, string scopeType, IReadOnlyCollection<Guid> permissionIds)
    {
        return new CreateCustomRoleRequest(name, "Role description.", scopeType, true, permissionIds);
    }

    private static UpdateCustomRoleRequest UpdateRoleRequest(
        string name = "Renamed Role",
        string? description = "Updated role.",
        bool isRequestable = true)
    {
        return new UpdateCustomRoleRequest(name, description, isRequestable);
    }

    private static CloneRoleRequest CloneRequest(string name)
    {
        return new CloneRoleRequest(name, "Cloned role.", true);
    }

    private static async Task<PermissionResponse> PlatformPermissionAsync(PermissionGraphApiFactory factory, string key)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var permission = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .SingleAsync(item => item.OrganizationId == null && item.Key == key);
        return new PermissionResponse(
            permission.Id,
            permission.OrganizationId,
            permission.Key,
            permission.DisplayName,
            permission.Description,
            permission.Module,
            permission.PermissionType.ToString(),
            permission.AllowedScopes.ToString(),
            permission.IsRequestable,
            permission.IsActive,
            permission.CreatedAtUtc,
            permission.UpdatedAtUtc,
            permission.ArchivedAtUtc);
    }

    private static async Task<RoleResponse> SystemRoleAsync(PermissionGraphApiFactory factory, Guid organizationId, string normalizedName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var role = await dbContext.Roles
            .AsNoTracking()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.OrganizationId == organizationId && item.NormalizedName == normalizedName);
        return new RoleResponse(
            role.Id,
            role.OrganizationId,
            role.Name,
            role.Description,
            role.RoleType.ToString(),
            role.ScopeType.ToString(),
            role.IsRequestable,
            role.IsActive,
            role.Permissions.Select(item => item.PermissionId).ToArray(),
            role.CreatedAtUtc,
            role.UpdatedAtUtc,
            role.ArchivedAtUtc,
            role.Version);
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
}
