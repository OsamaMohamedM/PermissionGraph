namespace PermissionGraph.IntegrationTests;

public sealed class AuthorizationEndpointTests : IAsyncLifetime
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
    public async Task AuthorizationEndpoints_ReturnUnauthorizedAnonymously()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();

        var check = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/authorization/check",
            new AuthorizationCheckRequest(null, null, "pg.projects.view"));
        var batch = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/authorization/batch-check",
            new AuthorizationBatchCheckRequest([
                new AuthorizationBatchCheckItemRequest("one", null, null, "pg.projects.view")
            ]));
        var explain = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/authorization/explain",
            new ExplainAccessRequest(null, null, "pg.projects.view", null));

        check.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        batch.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        explain.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OwnerCanCallPublicCheckAndReceivesDecisionContract()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "authz-api-owner@example.test");
        var organization = await CreateOrganizationAsync(client, "Authz Api Owner Org");
        var project = await CreateProjectAsync(client, organization.Id, "Authz Api Project");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/check",
            new AuthorizationCheckRequest(null, project.Id, "pg.projects.view"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>())!;
        decision.Allowed.Should().BeTrue();
        decision.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedOwnerOverride);
        decision.EvaluatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task PublicBatchCheck_ReturnsMixedOrderedDecisions()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "authz-api-batch-owner@example.test");
        var organization = await CreateOrganizationAsync(client, "Authz Api Batch Org");
        var project = await CreateProjectAsync(client, organization.Id, "Authz Batch Project");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/batch-check",
            new AuthorizationBatchCheckRequest(
            [
                new AuthorizationBatchCheckItemRequest("allowed", null, project.Id, "pg.projects.view"),
                new AuthorizationBatchCheckItemRequest("denied", null, project.Id, "pg.not_real.view")
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<AuthorizationBatchCheckResponse>())!;
        result.Items.Select(item => item.CorrelationId).Should().Equal("allowed", "denied");
        result.Items.Select(item => item.Index).Should().Equal(0, 1);
        result.Items[0].Decision.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedOwnerOverride);
        result.Items[1].Decision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);
    }

    [Fact]
    public async Task PermissionPolicyProtectsExistingMutationEndpointThroughEngine()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "authz-policy-owner@example.test");
        var projectAdministrator = await RegisterAndAuthorizeAsync(client, "authz-policy-pa@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Authz Policy Org");
        var project = await CreateProjectAsync(client, organization.Id, "Authz Policy Project");
        await AddMemberAsync(client, organization.Id, projectAdministrator.Email);

        await AuthorizeAsync(client, projectAdministrator.Email);
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/projects/{project.Id}",
            new UpdateProjectRequest("Policy Rename", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, "Access is forbidden.");
    }

    [Fact]
    public async Task PolicyDenialReturnsProblemDetailsBeforeHandlerRuns()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "authz-policy-deny-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "authz-policy-deny-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Authz Policy Deny Org");
        await AddMemberAsync(client, organization.Id, member.Email);

        await AuthorizeAsync(client, member.Email);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/permissions",
            new CreateCustomPermissionRequest("documents.view", "View documents", null, "Documents", "Organization", true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, "Access is forbidden.");
    }

    [Fact]
    public async Task ExplainSelfMatchesPublicCheckAndReturnsSafeSteps()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "explain-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "explain-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Explain Self Org");
        await AddMemberAsync(client, organization.Id, member.Email);
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");
        var checkPermission = await PlatformPermissionAsync(factory, "pg.authorization.check");
        var role = await CreateRoleAsync(client, organization.Id, "Document Reviewers", "Organization", [permission.Id, checkPermission.Id]);
        await CreateAssignmentAsync(client, organization.Id, member.UserId, role.Id, "Organization", organization.Id);

        await AuthorizeAsync(client, member.Email);
        var check = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/check",
            new AuthorizationCheckRequest(null, null, "documents.review"));
        var explain = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(null, null, "documents.review", null));

        check.StatusCode.Should().Be(HttpStatusCode.OK);
        explain.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = (await check.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>())!;
        var explanation = (await explain.Content.ReadFromJsonAsync<ExplainAccessResponse>())!;
        explanation.Allowed.Should().Be(decision.Allowed);
        explanation.ReasonCode.Should().Be(decision.ReasonCode);
        explanation.MatchedPath?.Type.Should().Be("RoleAssignment");
        explanation.Steps.Should().Contain(step => step.Code == "FINAL_DECISION");
        explanation.Steps.SelectMany(step => step.Details.Keys).Should().NotContain(key => key.Contains("cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OwnerExplainOtherSucceedsAndWritesAuditButNonOwnerIsForbidden()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "explain-other-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "explain-other-member@example.test");
        var outsider = await RegisterAndAuthorizeAsync(client, "explain-other-outsider@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Explain Other Org");
        await AddMemberAsync(client, organization.Id, member.Email);
        await AddMemberAsync(client, organization.Id, outsider.Email);

        var ownerExplain = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(member.UserId, null, "pg.projects.view", null));

        ownerExplain.StatusCode.Should().Be(HttpStatusCode.OK);
        var explanation = (await ownerExplain.Content.ReadFromJsonAsync<ExplainAccessResponse>())!;
        explanation.SubjectUserId.Should().Be(member.UserId);
        explanation.ActorUserId.Should().Be(owner.UserId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
            var auditCount = await dbContext.AuditLogs
                .CountAsync(item =>
                    item.OrganizationId == organization.Id &&
                    item.ActorUserId == owner.UserId &&
                    item.Action == "authorization.explain_other" &&
                    item.TargetId == member.UserId);
            auditCount.Should().BeGreaterThan(0);
        }

        await AuthorizeAsync(client, outsider.Email);
        var forbidden = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(member.UserId, null, "pg.projects.view", null));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(forbidden, "Actor is not allowed to explain another user's access.");
    }

    [Fact]
    public async Task DelegatedCheckOtherAndExplainOtherUsersConsistently()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "delegated-explain-owner@example.test");
        var subject = await RegisterAndAuthorizeAsync(client, "delegated-explain-subject@example.test");
        var delegated = await RegisterAndAuthorizeAsync(client, "delegated-explain-admin@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Delegated Explain Org");
        await AddMemberAsync(client, organization.Id, subject.Email);
        await AddMemberAsync(client, organization.Id, delegated.Email);
        var checkPermission = await PlatformPermissionAsync(factory, "pg.authorization.check");
        var checkOther = await PlatformPermissionAsync(factory, "pg.authorization.check_other_users");
        var explainOthers = await PlatformPermissionAsync(factory, "pg.authorization.explain_others");
        var role = await CreateRoleAsync(client, organization.Id, "Delegated Explainers", "Organization", [checkPermission.Id, checkOther.Id, explainOthers.Id]);
        await CreateAssignmentAsync(client, organization.Id, delegated.UserId, role.Id, "Organization", organization.Id);

        await AuthorizeAsync(client, delegated.Email);
        var check = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/check",
            new AuthorizationCheckRequest(subject.UserId, null, "pg.projects.view"));
        var explain = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(subject.UserId, null, "pg.projects.view", null));

        check.StatusCode.Should().Be(HttpStatusCode.OK);
        explain.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = (await check.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>())!;
        var explanation = (await explain.Content.ReadFromJsonAsync<ExplainAccessResponse>())!;
        decision.ReasonCode.Should().NotBe(AuthorizationReasonCode.DeniedCheckOtherUsersNotAllowed);
        explanation.Allowed.Should().Be(decision.Allowed);
        explanation.ReasonCode.Should().Be(decision.ReasonCode);
        explanation.ActorUserId.Should().Be(delegated.UserId);
        explanation.SubjectUserId.Should().Be(subject.UserId);
    }

    [Fact]
    public async Task DelegatedExplainOthersDoesNotAuthorizeCheckOtherUsers()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "delegated-explain-only-owner@example.test");
        var subject = await RegisterAndAuthorizeAsync(client, "delegated-explain-only-subject@example.test");
        var delegated = await RegisterAndAuthorizeAsync(client, "delegated-explain-only-admin@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Delegated Explain Only Org");
        await AddMemberAsync(client, organization.Id, subject.Email);
        await AddMemberAsync(client, organization.Id, delegated.Email);
        var checkPermission = await PlatformPermissionAsync(factory, "pg.authorization.check");
        var explainOthers = await PlatformPermissionAsync(factory, "pg.authorization.explain_others");
        var role = await CreateRoleAsync(client, organization.Id, "Delegated Explain Only", "Organization", [checkPermission.Id, explainOthers.Id]);
        await CreateAssignmentAsync(client, organization.Id, delegated.UserId, role.Id, "Organization", organization.Id);

        await AuthorizeAsync(client, delegated.Email);
        var check = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/check",
            new AuthorizationCheckRequest(subject.UserId, null, "pg.projects.view"));
        var explain = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(subject.UserId, null, "pg.projects.view", null));

        check.StatusCode.Should().Be(HttpStatusCode.OK);
        explain.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = (await check.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>())!;
        var explanation = (await explain.Content.ReadFromJsonAsync<ExplainAccessResponse>())!;
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedCheckOtherUsersNotAllowed);
        explanation.ActorUserId.Should().Be(delegated.UserId);
        explanation.SubjectUserId.Should().Be(subject.UserId);
    }

    [Fact]
    public async Task ExplainValidationErrorsUseProblemDetails()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        await RegisterAndAuthorizeAsync(client, "explain-validation@example.test");
        var organization = await CreateOrganizationAsync(client, "Explain Validation Org");

        var invalid = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/authorization/explain",
            new ExplainAccessRequest(null, Guid.Empty, "not valid", DateTimeOffset.UtcNow.AddMinutes(-1)));

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemAsync(invalid, "Request validation failed.");
    }

    private async Task<PermissionGraphApiFactory> CreateMigratedFactoryAsync()
    {
        var factory = new PermissionGraphApiFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PermissionGraph"] = _postgres!.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis!.GetConnectionString()
            });

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

    private static async Task<PermissionResponse> CreatePermissionAsync(
        HttpClient client,
        Guid organizationId,
        string key,
        string allowedScopes)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/permissions",
            new CreateCustomPermissionRequest(key, "Document permission", null, "Documents", allowedScopes, true));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PermissionResponse>())!;
    }

    private static async Task<RoleResponse> CreateRoleAsync(
        HttpClient client,
        Guid organizationId,
        string name,
        string scopeType,
        IReadOnlyCollection<Guid> permissionIds)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/roles",
            new CreateCustomRoleRequest(name, "Role description.", scopeType, true, permissionIds));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RoleResponse>())!;
    }

    private static async Task<RoleAssignmentResponse> CreateAssignmentAsync(
        HttpClient client,
        Guid organizationId,
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid scopeId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/role-assignments",
            new AssignRoleRequest(
                userId,
                roleId,
                scopeType,
                scopeId,
                DateTimeOffset.UtcNow.AddSeconds(-1),
                null,
                "Grant access for explanation test."));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RoleAssignmentResponse>())!;
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

    private static async Task AddProjectAdministratorAssignmentAsync(
        PermissionGraphApiFactory factory,
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid createdByUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var roleId = await dbContext.Roles
            .Where(role => role.OrganizationId == organizationId && role.NormalizedName == "PROJECT ADMINISTRATOR")
            .Select(role => role.Id)
            .SingleAsync();

        dbContext.ProjectAdministratorAssignments.Add(new ProjectAdministratorAssignmentRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProjectId = projectId,
            UserId = userId,
            RoleId = roleId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = createdByUserId
        });
        await dbContext.SaveChangesAsync();
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
