namespace PermissionGraph.IntegrationTests;

public sealed class AuthorizationReadServiceInfrastructureTests : IAsyncLifetime
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
    public async Task DbBackedOwnerOverrideData_AllowsOwnerThroughEvaluator()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-owner@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Owner Org");
        var project = await CreateProjectAsync(scope, organization.Id, "Owner Project");
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var decision = await service.CheckAsync(ProjectCheck(organization.Id, project.Id, "pg.projects.view"), CancellationToken.None);

        decision.Allowed.Should().BeTrue();
        decision.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedOwnerOverride);
    }

    [Fact]
    public async Task ActiveMemberWithoutGrant_DeniesThroughEvaluator()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-member-owner@example.test");
        var member = await CreateUserAsync(provider, "authz-member@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Member Org");
        await AddMemberAsync(scope, organization.Id, member.Email!);
        SetCurrentUser(provider, member.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var decision = await service.CheckAsync(OrganizationCheck(organization.Id, "pg.projects.create"), CancellationToken.None);

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task SuspendedRemovedAndNonMember_DenyMembership()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-membership-owner@example.test");
        var suspended = await CreateUserAsync(provider, "authz-suspended@example.test");
        var removed = await CreateUserAsync(provider, "authz-removed@example.test");
        var nonMember = await CreateUserAsync(provider, "authz-non-member@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Membership Org");
        await AddMemberAsync(scope, organization.Id, suspended.Email!);
        await AddMemberAsync(scope, organization.Id, removed.Email!);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.OrganizationMemberships.SingleAsync(item => item.OrganizationId == organization.Id && item.UserId == suspended.Id))
            .Suspend(isOwner: false, DateTimeOffset.UtcNow);
        (await dbContext.OrganizationMemberships.SingleAsync(item => item.OrganizationId == organization.Id && item.UserId == removed.Id))
            .Remove(isOwner: false, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        SetCurrentUser(provider, suspended.Id);
        var suspendedDecision = await service.CheckAsync(OrganizationCheck(organization.Id, "pg.projects.create"), CancellationToken.None);
        SetCurrentUser(provider, removed.Id);
        var removedDecision = await service.CheckAsync(OrganizationCheck(organization.Id, "pg.projects.create"), CancellationToken.None);
        SetCurrentUser(provider, nonMember.Id);
        var nonMemberDecision = await service.CheckAsync(OrganizationCheck(organization.Id, "pg.projects.create"), CancellationToken.None);

        suspendedDecision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedMembershipNotActive);
        removedDecision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedMembershipNotActive);
        nonMemberDecision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedMembershipNotActive);
    }

    [Fact]
    public async Task MissingAndInactiveData_MapsToStableDenialCodes()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-missing-owner@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Missing Org");
        var project = await CreateProjectAsync(scope, organization.Id, "Missing Project");
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();

        var missingOrganization = await service.CheckAsync(ProjectCheck(Guid.NewGuid(), project.Id, "pg.projects.view"), CancellationToken.None);
        var missingPermission = await service.CheckAsync(ProjectCheck(organization.Id, project.Id, "pg.not_real.view"), CancellationToken.None);
        var missingProject = await service.CheckAsync(ProjectCheck(organization.Id, Guid.NewGuid(), "pg.projects.view"), CancellationToken.None);

        var archivedOrganization = await dbContext.Organizations.SingleAsync(item => item.Id == organization.Id);
        archivedOrganization.Archive(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
        var inactiveOrganization = await service.CheckAsync(ProjectCheck(organization.Id, project.Id, "pg.projects.view"), CancellationToken.None);

        missingOrganization.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive);
        missingPermission.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);
        missingProject.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedProjectNotFoundOrInactive);
        inactiveOrganization.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive);
    }

    [Fact]
    public async Task ProjectMissingInactiveAndOutsideOrganization_AreDistinguished()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-project-owner@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Authz Project Org");
        var second = await CreateOrganizationAsync(scope, "Second Authz Project Org");
        var inactiveProject = await CreateProjectAsync(scope, first.Id, "Inactive Project");
        var outsideProject = await CreateProjectAsync(scope, second.Id, "Outside Project");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Projects.SingleAsync(item => item.Id == inactiveProject.Id)).Archive(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var inactive = await service.CheckAsync(ProjectCheck(first.Id, inactiveProject.Id, "pg.projects.view"), CancellationToken.None);
        var outside = await service.CheckAsync(ProjectCheck(first.Id, outsideProject.Id, "pg.projects.view"), CancellationToken.None);

        inactive.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedProjectNotFoundOrInactive);
        outside.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedProjectOutsideOrganization);
    }

    [Fact]
    public async Task ProjectAdministratorAssignmentPath_AllowsMatchingRolePermission()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-pa-owner@example.test");
        var projectAdministrator = await CreateUserAsync(provider, "authz-pa@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz PA Org");
        var project = await CreateProjectAsync(scope, organization.Id, "PA Project");
        await AddMemberAsync(scope, organization.Id, projectAdministrator.Email!);
        await AddProjectAdministratorAssignmentAsync(scope, organization.Id, project.Id, projectAdministrator.Id, owner.Id);
        SetCurrentUser(provider, projectAdministrator.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var decision = await service.CheckAsync(ProjectCheck(organization.Id, project.Id, "pg.projects.update"), CancellationToken.None);

        decision.Allowed.Should().BeTrue();
        decision.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedRolePermissionMatch);
    }

    [Fact]
    public async Task ProjectAdministratorAssignmentPath_DeniesWrongProjectInactiveRoleOrMissingPermission()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-pa-deny-owner@example.test");
        var projectAdministrator = await CreateUserAsync(provider, "authz-pa-deny@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz PA Deny Org");
        var assignedProject = await CreateProjectAsync(scope, organization.Id, "Assigned Project");
        var otherProject = await CreateProjectAsync(scope, organization.Id, "Other Project");
        await AddMemberAsync(scope, organization.Id, projectAdministrator.Email!);
        await AddProjectAdministratorAssignmentAsync(scope, organization.Id, assignedProject.Id, projectAdministrator.Id, owner.Id);
        SetCurrentUser(provider, projectAdministrator.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var wrongProject = await service.CheckAsync(ProjectCheck(organization.Id, otherProject.Id, "pg.projects.update"), CancellationToken.None);
        var missingPermission = await service.CheckAsync(ProjectCheck(organization.Id, assignedProject.Id, "pg.access_requests.create"), CancellationToken.None);

        SetCurrentUser(provider, owner.Id);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        await dbContext.Roles
            .Where(role => role.OrganizationId == organization.Id && role.NormalizedName == "PROJECT ADMINISTRATOR")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(role => role.IsActive, false)
                .SetProperty(role => role.ArchivedAtUtc, DateTimeOffset.UtcNow));
        SetCurrentUser(provider, projectAdministrator.Id);
        var inactiveRole = await service.CheckAsync(ProjectCheck(organization.Id, assignedProject.Id, "pg.projects.update"), CancellationToken.None);

        wrongProject.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
        missingPermission.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
        inactiveRole.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task ScopeCustomAndPlatformPermissionRules_AreTenantSafe()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-scope-owner@example.test");
        var member = await CreateUserAsync(provider, "authz-scope-member@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Authz Scope Org");
        var second = await CreateOrganizationAsync(scope, "Second Authz Scope Org");
        var project = await CreateProjectAsync(scope, first.Id, "Scope Project");
        await AddMemberAsync(scope, first.Id, member.Email!);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        dbContext.PermissionDefinitions.Add(PermissionDefinition.CreateCustom(
            Guid.NewGuid(),
            second.Id,
            "documents.approve",
            "documents.approve",
            "Approve documents",
            null,
            "Documents",
            PermissionAllowedScopes.Project,
            true,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        SetCurrentUser(provider, member.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var crossTenantCustom = await service.CheckAsync(ProjectCheck(first.Id, project.Id, "documents.approve"), CancellationToken.None);
        var platformScopeCompatible = await service.CheckAsync(ProjectCheck(first.Id, project.Id, "pg.projects.view"), CancellationToken.None);
        var scopeMismatch = await service.CheckAsync(ProjectCheck(first.Id, project.Id, "pg.projects.create"), CancellationToken.None);

        crossTenantCustom.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);
        platformScopeCompatible.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
        scopeMismatch.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedScopeMismatch);
    }

    [Fact]
    public async Task OrganizationScopeRequest_DoesNotUseProjectAdministratorPath()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-orgscope-owner@example.test");
        var projectAdministrator = await CreateUserAsync(provider, "authz-orgscope-pa@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Org Scope Org");
        var project = await CreateProjectAsync(scope, organization.Id, "Org Scope Project");
        await AddMemberAsync(scope, organization.Id, projectAdministrator.Email!);
        await AddProjectAdministratorAssignmentAsync(scope, organization.Id, project.Id, projectAdministrator.Id, owner.Id);
        SetCurrentUser(provider, projectAdministrator.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var decision = await service.CheckAsync(OrganizationCheck(organization.Id, "pg.projects.view"), CancellationToken.None);

        decision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task BatchRead_ReturnsMixedDecisionsWithoutCallingSingleReadPath()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "authz-batch-owner@example.test");
        var projectAdministrator = await CreateUserAsync(provider, "authz-batch-pa@example.test");
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Authz Batch Org");
        var project = await CreateProjectAsync(scope, organization.Id, "Batch Project");
        await AddMemberAsync(scope, organization.Id, projectAdministrator.Email!);
        await AddProjectAdministratorAssignmentAsync(scope, organization.Id, project.Id, projectAdministrator.Id, owner.Id);
        SetCurrentUser(provider, projectAdministrator.Id);
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationDecisionService>();

        var result = await service.BatchCheckAsync(
            new BatchCheckPermissionsQuery(
            [
                new BatchCheckPermissionItem("allow", null, organization.Id, project.Id, "pg.projects.update"),
                new BatchCheckPermissionItem("deny", null, organization.Id, project.Id, "pg.access_requests.create")
            ]),
            CancellationToken.None);

        result.Items.Select(item => item.CorrelationId).Should().Equal("allow", "deny");
        result.Items[0].Decision.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedRolePermissionMatch);
        result.Items[1].Decision.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public void ReadService_DoesNotExposeQueryableOrRedisDependency()
    {
        typeof(IAuthorizationReadService).GetMethods()
            .Select(method => method.ReturnType.FullName ?? method.ReturnType.Name)
            .Should()
            .NotContain(type => type.Contains("IQueryable", StringComparison.Ordinal));

        var service = typeof(InfrastructureServiceCollectionExtensions).Assembly
            .GetTypes()
            .Single(type => type.Name == "EfAuthorizationReadService");
        var serviceDescriptorTypes = service
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        serviceDescriptorTypes.Should().NotContain(type => type.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        serviceDescriptorTypes.Should().NotContain(type => type.Contains("Cache", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var currentUser = new MutableCurrentUser();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PermissionGraph"] = _postgres!.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis!.GetConnectionString(),
                ["Authentication:JwtSigningKey"] = "testing-jwt-signing-key-32-characters-minimum",
                ["Authentication:JwtIssuer"] = "PermissionGraph.Tests",
                ["Authentication:JwtAudience"] = "PermissionGraph.Tests",
                ["Authentication:JwtAccessTokenMinutes"] = "15",
                ["Authentication:RefreshTokenHashKey"] = "testing-refresh-hash-key-32-characters-minimum",
                ["Authentication:RefreshTokenDays"] = "30",
                ["Authentication:RequireConfirmedEmail"] = "false",
                ["Authentication:AutoConfirmEmail"] = "true",
                ["Authentication:NewUsersAreActive"] = "true"
            })
            .Build();

        services.AddSingleton(currentUser);
        services.AddSingleton<ICurrentUser>(currentUser);
        services.AddPermissionGraphApplication();
        services.AddPermissionGraphInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>().Database.MigrateAsync();
        CurrentUsers[provider] = currentUser;
        return provider;
    }

    private static readonly Dictionary<ServiceProvider, MutableCurrentUser> CurrentUsers = [];

    private static void SetCurrentUser(ServiceProvider provider, Guid userId)
    {
        CurrentUsers[provider].UserId = userId;
    }

    private static async Task<ApplicationUser> CreateUserAsync(ServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            DisplayName = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, "ValidPassword123!");
        result.Succeeded.Should().BeTrue();
        return user;
    }

    private static async Task<PermissionGraph.Application.Helper.Organizations.Models.OrganizationResult> CreateOrganizationAsync(
        IServiceScope scope,
        string name)
    {
        return await scope.ServiceProvider
            .GetRequiredService<CreateOrganizationHandler>()
            .HandleAsync(new CreateOrganizationCommand(name, null), CancellationToken.None);
    }

    private static async Task<PermissionGraph.Application.Helper.Projects.Models.ProjectResult> CreateProjectAsync(
        IServiceScope scope,
        Guid organizationId,
        string name)
    {
        return await scope.ServiceProvider
            .GetRequiredService<CreateProjectHandler>()
            .HandleAsync(new CreateProjectCommand(organizationId, name, null), CancellationToken.None);
    }

    private static async Task AddMemberAsync(IServiceScope scope, Guid organizationId, string email)
    {
        await scope.ServiceProvider
            .GetRequiredService<AddOrganizationMemberHandler>()
            .HandleAsync(new AddOrganizationMemberCommand(organizationId, email), CancellationToken.None);
    }

    private static async Task AddProjectAdministratorAssignmentAsync(
        IServiceScope scope,
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid createdByUserId)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var roleId = await dbContext.Roles
            .Where(role =>
                role.OrganizationId == organizationId &&
                role.NormalizedName == "PROJECT ADMINISTRATOR" &&
                role.ScopeType == RoleScopeType.Project)
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

    private static CheckPermissionQuery ProjectCheck(Guid organizationId, Guid projectId, string permissionKey)
    {
        return new CheckPermissionQuery(null, organizationId, projectId, permissionKey);
    }

    private static CheckPermissionQuery OrganizationCheck(Guid organizationId, string permissionKey)
    {
        return new CheckPermissionQuery(null, organizationId, null, permissionKey);
    }

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }
}
