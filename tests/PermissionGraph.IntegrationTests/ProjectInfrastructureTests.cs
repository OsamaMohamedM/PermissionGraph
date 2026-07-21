namespace PermissionGraph.IntegrationTests;

public sealed class ProjectInfrastructureTests : IAsyncLifetime
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
    public async Task CreateProject_PersistsProjectAdministratorAssignmentAndAuditAtomically()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-create@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Project Create Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();

        var project = await createProject.HandleAsync(
            new CreateProjectCommand(organization.Id, "Launch Control", "Coordinate launch."),
            CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Projects.CountAsync()).Should().Be(1);
        (await dbContext.ProjectAdministratorAssignments.CountAsync()).Should().Be(1);
        var assignment = await dbContext.ProjectAdministratorAssignments.SingleAsync();
        assignment.OrganizationId.Should().Be(organization.Id);
        assignment.ProjectId.Should().Be(project.Id);
        assignment.UserId.Should().Be(owner.Id);
        (await dbContext.Roles.AnyAsync(role => role.Id == assignment.RoleId && role.NormalizedName == "PROJECT ADMINISTRATOR")).Should().BeTrue();
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action == "project.administrator_assigned")).Should().Be(1);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action == "project.created")).Should().Be(1);
    }

    [Fact]
    public async Task ProjectRepository_UsesOrganizationScopedLookupAndListing()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-scope@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Project Scope Org");
        var second = await CreateOrganizationAsync(scope, "Second Project Scope Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
        var firstProject = await createProject.HandleAsync(new CreateProjectCommand(first.Id, "Shared Name", null), CancellationToken.None);
        var secondProject = await createProject.HandleAsync(new CreateProjectCommand(second.Id, "Shared Name", null), CancellationToken.None);
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var mismatch = await repository.GetByOrganizationAndIdAsync(first.Id, secondProject.Id, CancellationToken.None);
        var list = await repository.ListPageForOrganizationAsync(first.Id, page: 1, pageSize: 100, CancellationToken.None);

        mismatch.Should().BeNull();
        list.Items.Should().ContainSingle(project => project.Id == firstProject.Id);
    }

    [Fact]
    public async Task ActiveNormalizedName_IsUniqueWithinOrganizationButAllowedAcrossOrganizationsAndAfterArchive()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-unique@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Unique Project Org");
        var second = await CreateOrganizationAsync(scope, "Second Unique Project Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
        var archiveProject = scope.ServiceProvider.GetRequiredService<ArchiveProjectHandler>();

        var project = await createProject.HandleAsync(new CreateProjectCommand(first.Id, "Launch Control", null), CancellationToken.None);
        await createProject.HandleAsync(new CreateProjectCommand(second.Id, "launch control", null), CancellationToken.None);

        var duplicate = () => createProject.HandleAsync(new CreateProjectCommand(first.Id, " launch control ", null), CancellationToken.None);

        await duplicate.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "project_name_already_exists");

        await archiveProject.HandleAsync(new ArchiveProjectCommand(first.Id, project.Id, "ARCHIVE"), CancellationToken.None);
        var replacement = await createProject.HandleAsync(new CreateProjectCommand(first.Id, "launch control", null), CancellationToken.None);

        replacement.Id.Should().NotBe(project.Id);
    }

    [Fact]
    public async Task DatabaseConstraint_PreventsRacingDuplicateActiveProjectNames()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-db-unique@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Database Unique Project Org");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        dbContext.Projects.Add(Project.Create(Guid.NewGuid(), organization.Id, "Billing Portal", "BILLING PORTAL", null, DateTimeOffset.UtcNow));
        dbContext.Projects.Add(Project.Create(Guid.NewGuid(), organization.Id, "billing portal", "BILLING PORTAL", null, DateTimeOffset.UtcNow));

        var save = () => dbContext.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AssignmentFailure_RollsBackProject()
    {
        await using var provider = await CreateProviderAsync(services =>
            services.AddScoped<IProjectAdministratorAssignmentService, ThrowingProjectAdministratorAssignmentService>());
        var owner = await CreateUserAsync(provider, "project-owner-assignment-fail@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Assignment Failure Project Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();

        var failedCreate = () => createProject.HandleAsync(new CreateProjectCommand(organization.Id, "Launch Control", null), CancellationToken.None);

        await failedCreate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("assignment failure");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Projects.CountAsync()).Should().Be(0);
        (await dbContext.ProjectAdministratorAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditFailure_RollsBackProjectAndAssignment()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-audit-fail@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        using (var scope = provider.CreateScope())
        {
            var organization = await CreateOrganizationAsync(scope, "Audit Failure Project Org");
            organizationId = organization.Id;
        }

        await using var failingProvider = await CreateProviderAsync(services =>
            services.AddScoped<IAuditWriter, ThrowingAuditWriter>());
        SetCurrentUser(failingProvider, owner.Id);

        using var failingScope = failingProvider.CreateScope();
        var createProject = failingScope.ServiceProvider.GetRequiredService<CreateProjectHandler>();

        var failedCreate = () => createProject.HandleAsync(new CreateProjectCommand(organizationId, "Launch Control", null), CancellationToken.None);

        await failedCreate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failure");
        var dbContext = failingScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Projects.CountAsync()).Should().Be(0);
        (await dbContext.ProjectAdministratorAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateAndArchiveProject_PersistStatusVersionAndAudit()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-mutate@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Mutate Project Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
        var updateProject = scope.ServiceProvider.GetRequiredService<UpdateProjectHandler>();
        var archiveProject = scope.ServiceProvider.GetRequiredService<ArchiveProjectHandler>();
        var project = await createProject.HandleAsync(new CreateProjectCommand(organization.Id, "Launch Control", null), CancellationToken.None);

        var updated = await updateProject.HandleAsync(new UpdateProjectCommand(organization.Id, project.Id, "Billing Portal", "Updated"), CancellationToken.None);
        await archiveProject.HandleAsync(new ArchiveProjectCommand(organization.Id, project.Id, "ARCHIVE"), CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var persisted = await dbContext.Projects.AsNoTracking().SingleAsync(item => item.Id == project.Id);
        updated.Version.Should().BeGreaterThan(project.Version);
        persisted.Status.Should().Be(ProjectStatus.Archived);
        persisted.ArchivedAtUtc.Should().NotBeNull();
        persisted.Version.Should().BeGreaterThan(updated.Version);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action == "project.updated")).Should().Be(1);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action == "project.archived")).Should().Be(1);
    }

    [Fact]
    public async Task ArchivedProjectMutation_ReturnsConflict()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-archived-conflict@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Archived Mutation Project Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
        var updateProject = scope.ServiceProvider.GetRequiredService<UpdateProjectHandler>();
        var archiveProject = scope.ServiceProvider.GetRequiredService<ArchiveProjectHandler>();
        var project = await createProject.HandleAsync(new CreateProjectCommand(organization.Id, "Launch Control", null), CancellationToken.None);
        await archiveProject.HandleAsync(new ArchiveProjectCommand(organization.Id, project.Id, "ARCHIVE"), CancellationToken.None);

        var update = () => updateProject.HandleAsync(new UpdateProjectCommand(organization.Id, project.Id, "Billing Portal", null), CancellationToken.None);

        await update.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "archived_project_cannot_be_updated");
    }

    [Fact]
    public async Task ProjectVersion_ProducesConcurrencyConflict()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-concurrency@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        Guid projectId;
        using (var scope = provider.CreateScope())
        {
            var organization = await CreateOrganizationAsync(scope, "Concurrency Project Org");
            var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
            var project = await createProject.HandleAsync(new CreateProjectCommand(organization.Id, "Launch Control", null), CancellationToken.None);
            organizationId = organization.Id;
            projectId = project.Id;
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var first = await firstContext.Projects.SingleAsync(item => item.OrganizationId == organizationId && item.Id == projectId);
        var second = await secondContext.Projects.SingleAsync(item => item.OrganizationId == organizationId && item.Id == projectId);

        first.UpdateDetails("First Update", "FIRST UPDATE", null, DateTimeOffset.UtcNow);
        second.UpdateDetails("Second Update", "SECOND UPDATE", null, DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync();

        var concurrentSave = () => secondContext.SaveChangesAsync();

        await concurrentSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Seed_IsDuplicateSafeAndAddsProjectPermissionsAndRole()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-seed@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Seed Project Org");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<PermissionGraph.Application.Abstractions.Services.Organizations.IOrganizationSeedService>();

        await seed.SeedDefaultAuthorizationAsync(
            await dbContext.Organizations.SingleAsync(item => item.Id == organization.Id),
            owner.Id,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var projectPermissions = await dbContext.PermissionDefinitions
            .Where(permission => permission.Key.StartsWith("pg.projects."))
            .Select(permission => permission.Key)
            .ToListAsync();

        projectPermissions.Should().BeEquivalentTo(
            "pg.projects.create",
            "pg.projects.view",
            "pg.projects.update",
            "pg.projects.archive");
        var projectAdministratorRole = await dbContext.Roles.SingleAsync(role =>
            role.OrganizationId == organization.Id &&
            role.NormalizedName == "PROJECT ADMINISTRATOR" &&
            role.ScopeType == "Project");
        var projectAdministratorPermissions = await (
            from rolePermission in dbContext.RolePermissions
            join permission in dbContext.PermissionDefinitions on rolePermission.PermissionId equals permission.Id
            where rolePermission.RoleId == projectAdministratorRole.Id
            select permission.Key)
            .ToListAsync();

        projectAdministratorPermissions.Should().Contain(
            "pg.projects.view",
            "pg.projects.update",
            "pg.projects.archive");
    }

    [Fact]
    public async Task RestrictiveForeignKeys_PreventInvalidProjectAndCrossTenantAssignmentRelationships()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "project-owner-fk@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First FK Project Org");
        var second = await CreateOrganizationAsync(scope, "Second FK Project Org");
        var createProject = scope.ServiceProvider.GetRequiredService<CreateProjectHandler>();
        var project = await createProject.HandleAsync(new CreateProjectCommand(first.Id, "Launch Control", null), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();

        dbContext.ChangeTracker.Clear();
        dbContext.Projects.Add(Project.Create(Guid.NewGuid(), Guid.NewGuid(), "Missing Org", "MISSING ORG", null, DateTimeOffset.UtcNow));
        await FluentActions.Invoking(() => dbContext.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();

        dbContext.ChangeTracker.Clear();
        var otherRole = await dbContext.Roles.AsNoTracking().SingleAsync(role => role.OrganizationId == second.Id && role.NormalizedName == "PROJECT ADMINISTRATOR");
        dbContext.ProjectAdministratorAssignments.Add(new ProjectAdministratorAssignmentRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = first.Id,
            ProjectId = project.Id,
            UserId = owner.Id,
            RoleId = otherRole.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = owner.Id
        });

        await FluentActions.Invoking(() => dbContext.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<ServiceProvider> CreateProviderAsync(Action<IServiceCollection>? configureServices = null)
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
        configureServices?.Invoke(services);

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

    private static async Task<ApplicationUser> CreateUserAsync(ServiceProvider provider, string email, bool isActive)
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
            IsActive = isActive,
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
        var createOrganization = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        return await createOrganization.HandleAsync(new CreateOrganizationCommand(name, null), CancellationToken.None);
    }

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }

    private sealed class ThrowingProjectAdministratorAssignmentService : IProjectAdministratorAssignmentService
    {
        public Task AssignCreatorAsProjectAdministratorAsync(Project project, Guid creatorUserId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("assignment failure");
        }
    }

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("audit failure");
        }
    }
}