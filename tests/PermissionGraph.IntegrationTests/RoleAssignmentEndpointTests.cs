namespace PermissionGraph.IntegrationTests;

public sealed class RoleAssignmentEndpointTests : IAsyncLifetime
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
    public async Task RoleAssignmentEndpoints_ReturnUnauthorizedAnonymously()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/v1/organizations/{organizationId}/role-assignments"),
            await client.GetAsync($"/api/v1/organizations/{organizationId}/role-assignments/{assignmentId}"),
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/role-assignments", AssignRequest(Guid.NewGuid(), Guid.NewGuid(), "Organization", organizationId)),
            await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/role-assignments/{assignmentId}/revoke", new RevokeRoleAssignmentRequest("No longer needed."))
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task OwnerAssignsListsGetsAndRevokesOrganizationRole()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "assignment-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "assignment-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Assignment Api Org");
        await AddMemberAsync(client, organization.Id, member.Email);
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.review", "Organization");
        var role = await CreateRoleAsync(client, organization.Id, "Document Reviewers", "Organization", [permission.Id]);

        var create = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/role-assignments",
            AssignRequest(member.UserId, role.Id, "Organization", organization.Id));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location?.ToString().Should().StartWith($"/api/v1/organizations/{organization.Id}/role-assignments/");
        var assignment = (await create.Content.ReadFromJsonAsync<RoleAssignmentResponse>())!;
        assignment.UserId.Should().Be(member.UserId);
        assignment.RoleId.Should().Be(role.Id);
        assignment.ScopeType.Should().Be("Organization");
        assignment.ScopeId.Should().Be(organization.Id);
        assignment.Status.Should().Be("Active");

        var get = await client.GetAsync(create.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadFromJsonAsync<RoleAssignmentResponse>())!.Id.Should().Be(assignment.Id);

        var list = await client.GetAsync($"/api/v1/organizations/{organization.Id}/role-assignments?userId={member.UserId}&status=Active&pageSize=100");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<RoleAssignmentListResponse>())!.Items.Should().ContainSingle(item => item.Id == assignment.Id);

        var revoke = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/role-assignments/{assignment.Id}/revoke",
            new RevokeRoleAssignmentRequest("Access window ended."));
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        (await revoke.Content.ReadFromJsonAsync<RoleAssignmentResponse>())!.Status.Should().Be("Revoked");
    }

    [Fact]
    public async Task ScheduledAndTemporaryAssignmentsRespectAuthorizationBoundaries()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "assignment-temp-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "assignment-temp-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Assignment Temp Org");
        await AddMemberAsync(client, organization.Id, member.Email);
        var project = await CreateProjectAsync(client, organization.Id, "Assignment Temp Project");
        var permission = await CreatePermissionAsync(client, organization.Id, "documents.approve", "Project");
        var role = await CreateRoleAsync(client, organization.Id, "Project Approvers", "Project", [permission.Id]);

        var futureStart = DateTimeOffset.UtcNow.AddHours(1);
        var scheduled = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/role-assignments",
            AssignRequest(member.UserId, role.Id, "Project", project.Id, futureStart, futureStart.AddHours(1)));
        scheduled.StatusCode.Should().Be(HttpStatusCode.Created);

        var beforeStart = await CheckAsync(client, organization.Id, member.UserId, project.Id, "documents.approve");
        beforeStart.Allowed.Should().BeFalse();
        beforeStart.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);

        await AuthorizeAsync(client, owner.Email);
        var expiredStart = DateTimeOffset.UtcNow.AddMinutes(-10);
        var expired = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/role-assignments",
            AssignRequest(member.UserId, role.Id, "Project", project.Id, expiredStart, DateTimeOffset.UtcNow.AddSeconds(-1)));
        expired.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DuplicateSelfAssignmentValidationAndCrossTenantAccessAreBlocked()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "assignment-security-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "assignment-security-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var first = await CreateOrganizationAsync(client, "Assignment Security First Org");
        var second = await CreateOrganizationAsync(client, "Assignment Security Second Org");
        await AddMemberAsync(client, first.Id, member.Email);
        await AddMemberAsync(client, second.Id, member.Email);
        var firstPermission = await CreatePermissionAsync(client, first.Id, "documents.review", "Organization");
        var secondPermission = await CreatePermissionAsync(client, second.Id, "documents.review", "Organization");
        var firstRole = await CreateRoleAsync(client, first.Id, "Document Reviewers", "Organization", [firstPermission.Id]);
        var secondRole = await CreateRoleAsync(client, second.Id, "Document Reviewers", "Organization", [secondPermission.Id]);
        var assignment = await CreateAssignmentAsync(client, first.Id, member.UserId, firstRole.Id, "Organization", first.Id);

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/role-assignments",
            AssignRequest(member.UserId, firstRole.Id, "Organization", first.Id));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.GetAsync($"/api/v1/organizations/{first.Id}/role-assignments/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/organizations/{first.Id}/role-assignments/{assignment.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthorizeAsync(client, member.Email);
        var selfAssign = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/role-assignments",
            AssignRequest(member.UserId, firstRole.Id, "Organization", first.Id));
        selfAssign.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthorizeAsync(client, owner.Email);
        var crossTenantRole = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{first.Id}/role-assignments",
            AssignRequest(member.UserId, secondRole.Id, "Organization", first.Id));
        crossTenantRole.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonOwnerSelfAssignmentDenialPersistsAuditWithoutCreatingForbiddenAssignment()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();
        var owner = await RegisterAndAuthorizeAsync(client, "assignment-audit-owner@example.test");
        var member = await RegisterAndAuthorizeAsync(client, "assignment-audit-member@example.test");

        await AuthorizeAsync(client, owner.Email);
        var organization = await CreateOrganizationAsync(client, "Assignment Audit Org");
        await AddMemberAsync(client, organization.Id, member.Email);
        var assignPermission = await PlatformPermissionAsync(factory, "pg.roles.assign");
        var grantRole = await CreateRoleAsync(client, organization.Id, "Assignment Delegates", "Organization", [assignPermission.Id]);
        var targetPermission = await CreatePermissionAsync(client, organization.Id, "documents.audit.review", "Organization");
        var targetRole = await CreateRoleAsync(client, organization.Id, "Audit Reviewers", "Organization", [targetPermission.Id]);
        await CreateAssignmentAsync(client, organization.Id, member.UserId, grantRole.Id, "Organization", organization.Id);

        await AuthorizeAsync(client, member.Email);
        var denied = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organization.Id}/role-assignments",
            AssignRequest(member.UserId, targetRole.Id, "Organization", organization.Id));

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var forbiddenAssignments = await dbContext.RoleAssignments.CountAsync(item =>
            item.OrganizationId == organization.Id &&
            item.UserId == member.UserId &&
            item.RoleId == targetRole.Id);
        var auditCount = await dbContext.AuditLogs.CountAsync(item =>
            item.OrganizationId == organization.Id &&
            item.ActorUserId == member.UserId &&
            item.Action == "role_assignment.privilege_escalation_denied" &&
            item.TargetId == targetRole.Id &&
            item.Result == "Failed");

        forbiddenAssignments.Should().Be(0);
        auditCount.Should().BeGreaterThan(0);
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

    private static async Task AddMemberAsync(HttpClient client, Guid organizationId, string email)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/organizations/{organizationId}/members", new AddOrganizationMemberRequest(email));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, Guid organizationId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/projects",
            new CreateProjectRequest(name, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
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
            AssignRequest(userId, roleId, scopeType, scopeId));
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

    private static AssignRoleRequest AssignRequest(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid scopeId,
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        return new AssignRoleRequest(
            userId,
            roleId,
            scopeType,
            scopeId,
            startsAtUtc ?? DateTimeOffset.UtcNow.AddSeconds(-1),
            expiresAtUtc,
            "Temporary access for project delivery.");
    }

    private static async Task<AuthorizationDecisionResponse> CheckAsync(
        HttpClient client,
        Guid organizationId,
        Guid? subjectUserId,
        Guid? projectId,
        string permissionKey)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/authorization/check",
            new AuthorizationCheckRequest(subjectUserId, projectId, permissionKey));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>())!;
    }
}
