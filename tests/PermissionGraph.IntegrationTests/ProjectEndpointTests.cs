namespace PermissionGraph.IntegrationTests;

public sealed class ProjectEndpointTests : IAsyncLifetime
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
    public async Task ProjectEndpoints_ReturnUnauthorizedAnonymously()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var responses = new[]
        {
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/projects", new CreateProjectRequest("Launch Control", null)),
            await client.GetAsync($"/api/v1/organizations/{organizationId}/projects"),
            await client.GetAsync($"/api/v1/organizations/{organizationId}/projects/{projectId}"),
            await client.PatchAsJsonAsync($"/api/v1/organizations/{organizationId}/projects/{projectId}", new UpdateProjectRequest("Launch Control", null)),
            await client.PostAsync($"/api/v1/organizations/{organizationId}/projects/{projectId}/archive", null)
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task OwnerCreate_ReturnsCreatedLocationAssignmentAndAudit()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "project-owner-create@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Create Api Org");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects",
            new CreateProjectRequest("Launch Control", "Coordinate launch."));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location?.ToString().Should().StartWith($"/api/v1/organizations/{organization.Id}/projects/");
        var project = (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
        project.OrganizationId.Should().Be(organization.Id);
        project.Status.Should().Be("Active");

        var get = await client.GetAsync(response.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = (await get.Content.ReadFromJsonAsync<ProjectResponse>())!;
        fetched.Id.Should().Be(project.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.ProjectAdministratorAssignments.CountAsync(item => item.ProjectId == project.Id && item.UserId == owner.UserId)).Should().Be(1);
        (await dbContext.AuditLogs.CountAsync(item => item.TargetId == project.Id && item.Action == "project.created")).Should().Be(1);
    }

    [Fact]
    public async Task ActiveMemberCanListAndGetOnlyRouteOrganizationProjects()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "project-owner-member-visible@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "project-member-visible@example.test");

        await AuthorizeAsync(client, owner.Email);
        var first = await CreateOrganizationAsync(client, "Project Visible First Org");
        var second = await CreateOrganizationAsync(client, "Project Visible Second Org");
        var firstProject = await CreateProjectAsync(client, first.Id, "Visible Project");
        var secondProject = await CreateProjectAsync(client, second.Id, "Hidden Project");
        await AddMemberAsync(client, first.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        var list = await client.GetAsync($"/api/v1/organizations/{first.Id}/projects");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await list.Content.ReadFromJsonAsync<ProjectListResponse>())!;
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(item => item.Id == firstProject.Id);
        body.Items.Should().NotContain(item => item.Id == secondProject.Id);

        var get = await client.GetAsync($"/api/v1/organizations/{first.Id}/projects/{firstProject.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var mismatch = await client.GetAsync($"/api/v1/organizations/{first.Id}/projects/{secondProject.Id}");
        mismatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemAsync(mismatch, "Project could not be found.");
    }

    [Fact]
    public async Task SuspendedOrRemovedMemberCannotListOrGetProjects()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "project-owner-member-states@example.test");
        var suspendedMember = await RegisterAndAuthorizeAsync(client, "project-suspended-member@example.test");
        var removedMember = await RegisterAndAuthorizeAsync(client, "project-removed-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Project Membership State Org");
        var project = await CreateProjectAsync(client, organization.Id, "Membership State Project");
        await AddMemberAsync(client, organization.Id, suspendedMember.Email);
        await AddMemberAsync(client, organization.Id, removedMember.Email);
        (await client.PostAsync($"/api/v1/organizations/{organization.Id}/members/{suspendedMember.UserId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/organizations/{organization.Id}/members/{removedMember.UserId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AuthorizeAsync(client, suspendedMember.Email);
        var suspendedList = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects");
        suspendedList.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var suspendedGet = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}");
        suspendedGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthorizeAsync(client, removedMember.Email);
        var removedList = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects");
        removedList.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var removedGet = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}");
        removedGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonOwnerCannotCreateUpdateOrArchive()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "project-owner-forbidden@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "project-member-forbidden@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Project Forbidden Org");
        var project = await CreateProjectAsync(client, organization.Id, "Owner Project");
        await AddMemberAsync(client, organization.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        var create = await client.PostAsJsonAsync($"/api/v1/organizations/{organization.Id}/projects", new CreateProjectRequest("Member Project", null));
        var update = await client.PatchAsJsonAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}", new UpdateProjectRequest("Member Rename", null));
        var archive = await client.PostAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}/archive", null);

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        archive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OwnerCanUpdateAndArchiveArchivedProjectIsNotReadableAndRejectsMutation()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-mutate@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Mutate Api Org");
        var project = await CreateProjectAsync(client, organization.Id, "Mutable Project");

        var update = await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects/{project.Id}",
            new UpdateProjectRequest("Renamed Project", "Updated"));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<ProjectResponse>())!;
        updated.Name.Should().Be("Renamed Project");

        var archive = await client.PostAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await archive.Content.ReadAsStringAsync()).Should().BeEmpty();

        var getArchived = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects/{project.Id}");
        getArchived.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateArchived = await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects/{project.Id}",
            new UpdateProjectRequest("Renamed Again", null));
        updateArchived.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(updateArchived, "Archived project cannot be updated.");
    }

    [Fact]
    public async Task DuplicateActiveNameConflictsArchivedNameReuseAndOtherOrganizationNameSucceed()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-names@example.test");
        var first = await CreateOrganizationAsync(client, "Project Names First Org");
        var second = await CreateOrganizationAsync(client, "Project Names Second Org");
        var project = await CreateProjectAsync(client, first.Id, "Launch Control");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/projects",
            new CreateProjectRequest(" launch control ", null));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(duplicate, "An active project with this name already exists.");

        var otherOrganization = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{second.Id}/projects",
            new CreateProjectRequest("launch control", null));
        otherOrganization.StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsync($"/api/v1/organizations/{first.Id}/projects/{project.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var replacement = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/projects",
            new CreateProjectRequest("launch control", null));
        replacement.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PaginationAndRequestValidationUseProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-pagination@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Pagination Org");
        await CreateProjectAsync(client, organization.Id, "First Page Project");
        await CreateProjectAsync(client, organization.Id, "Second Page Project");

        var firstPage = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects?page=1&pageSize=1");
        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await firstPage.Content.ReadFromJsonAsync<ProjectListResponse>())!;
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().Be(2);
        page.Items.Should().HaveCount(1);

        var max = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects?pageSize=100");
        max.StatusCode.Should().Be(HttpStatusCode.OK);
        (await max.Content.ReadFromJsonAsync<ProjectListResponse>())!.PageSize.Should().Be(100);

        var invalidList = await client.GetAsync($"/api/v1/organizations/{organization.Id}/projects?page=0&pageSize=101");
        invalidList.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var listProblem = await AssertProblemAsync(invalidList, "Request validation failed.");
        TryGetErrors(listProblem, out var errors).Should().BeTrue();
        errors.TryGetProperty("Page", out _).Should().BeTrue();
        errors.TryGetProperty("PageSize", out _).Should().BeTrue();

        var invalidCreate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects",
            new CreateProjectRequest("ab", new string('x', 2001)));
        invalidCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var createProblem = await AssertProblemAsync(invalidCreate, "Request validation failed.");
        TryGetErrors(createProblem, out var createErrors).Should().BeTrue();
        createErrors.TryGetProperty("Name", out _).Should().BeTrue();
        createErrors.TryGetProperty("Description", out _).Should().BeTrue();
    }

    [Fact]
    public async Task StaleConcurrencyUpdate_ReturnsConflictProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-concurrency@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Concurrency Api Org");
        var project = await CreateProjectAsync(client, organization.Id, "Concurrency Project");

        using var conflictFactory = await CreateMigratedFactoryAsync(services =>
        {
            services.RemoveAll<IAuditWriter>();
            services.AddScoped<IAuditWriter, ConcurrentProjectUpdateAuditWriter>();
        });
        using var conflictClient = conflictFactory.CreateClient();
        conflictClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var response = await conflictClient.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects/{project.Id}",
            new UpdateProjectRequest("Concurrency Rename", null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemAsync(response, "The resource was modified by another request.");
    }

    [Fact]
    public async Task ProjectMutationRateLimit_ReturnsProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-rate-limit@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Rate Limit Org");

        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 31; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/organizations/{organization.Id}/projects",
                new CreateProjectRequest($"Rate Project {attempt}", null));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }
        }

        limited.Should().NotBeNull();
        await AssertProblemAsync(limited!, "Too many requests.");
    }

    [Fact]
    public async Task ErrorProblemDetailsAreSafe()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "project-owner-safe-error@example.test");
        var organization = await CreateOrganizationAsync(client, "Project Safe Error Org");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects",
            new CreateProjectRequest("Safe Project", "database stack trace token hash password secret"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects",
            new CreateProjectRequest("safe project", "database stack trace token hash password secret"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = (await duplicate.Content.ReadAsStringAsync()).ToUpperInvariant();
        body.Should().NotContain("NPGSQL");
        body.Should().NotContain("STACK");
        body.Should().NotContain("TOKEN");
        body.Should().NotContain("HASH");
        body.Should().NotContain("PASSWORD");
        body.Should().NotContain("SECRET");
        body.Should().NotContain("DATABASE");
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

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, Guid organizationId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/projects", new CreateProjectRequest(name, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }

    private static async Task AddMemberAsync(HttpClient client, Guid organizationId, string email)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/members", new AddOrganizationMemberRequest(email));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
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

    private sealed class ConcurrentProjectUpdateAuditWriter(IServiceScopeFactory scopeFactory) : IAuditWriter
    {
        public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            if (record.Action != "project.updated")
            {
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
            var project = await dbContext.Projects.SingleAsync(item => item.Id == record.TargetId, cancellationToken);
            project.UpdateDetails("Concurrent Project Rename", "CONCURRENT PROJECT RENAME", project.Description, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
