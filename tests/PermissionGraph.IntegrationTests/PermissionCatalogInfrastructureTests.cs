namespace PermissionGraph.IntegrationTests;

public sealed class PermissionCatalogInfrastructureTests : IAsyncLifetime
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
    public async Task PlatformCatalogSeed_IsIdempotentAndBackfillsRolePermissionMappings()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-seed@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Permission Seed Org");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<PermissionGraph.Application.Abstractions.Services.Organizations.IOrganizationSeedService>();

        await seed.SeedDefaultAuthorizationAsync(
            await dbContext.Organizations.SingleAsync(item => item.Id == organization.Id),
            owner.Id,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var platformPermissions = await dbContext.PermissionDefinitions
            .Where(permission => permission.OrganizationId == null && permission.PermissionType == PermissionType.Platform)
            .Select(permission => new { permission.Id, permission.Key, permission.NormalizedKey })
            .ToListAsync();
        var duplicateMappings = await dbContext.RolePermissions
            .GroupBy(mapping => new { mapping.RoleId, mapping.PermissionId })
            .Where(group => group.Count() > 1)
            .CountAsync();

        platformPermissions.Should().HaveCount(31);
        platformPermissions.Should().OnlyContain(permission => permission.Key == permission.NormalizedKey);
        platformPermissions.Select(permission => permission.Id).Should().OnlyHaveUniqueItems();
        duplicateMappings.Should().Be(0);
        (await RolePermissionKeysAsync(dbContext, organization.Id, "ORGANIZATION ADMINISTRATOR", "Organization"))
            .Should()
            .Contain(["pg.permissions.view", "pg.permissions.create", "pg.permissions.update", "pg.permissions.archive"]);
    }

    [Fact]
    public async Task CustomPermissionPersistence_AllowsSameKeyAcrossOrganizationsButBlocksDuplicateWithinOrganization()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-unique@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Permission Unique Org");
        var second = await CreateOrganizationAsync(scope, "Second Permission Unique Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();

        await createPermission.HandleAsync(CreateCommand(first.Id, "billing.invoice.view"), CancellationToken.None);
        await createPermission.HandleAsync(CreateCommand(second.Id, "billing.invoice.view"), CancellationToken.None);
        var duplicate = () => createPermission.HandleAsync(CreateCommand(first.Id, "billing.invoice.view"), CancellationToken.None);

        await duplicate.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "permission_key_already_exists");
    }

    [Fact]
    public async Task RepositoryVisibility_CombinesPlatformAndRouteCustomWithoutCrossTenantLeakage()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-visibility@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Permission Visibility Org");
        var second = await CreateOrganizationAsync(scope, "Second Permission Visibility Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var firstCustom = await createPermission.HandleAsync(CreateCommand(first.Id, "billing.invoice.view"), CancellationToken.None);
        var secondCustom = await createPermission.HandleAsync(CreateCommand(second.Id, "billing.invoice.view"), CancellationToken.None);
        var repository = scope.ServiceProvider.GetRequiredService<IPermissionDefinitionRepository>();

        var firstList = await repository.ListVisibleForOrganizationAsync(
            first.Id,
            new PermissionDefinitionListFilters(null, null, null, null, null, null),
            page: 1,
            pageSize: 100,
            CancellationToken.None);
        var crossTenantGet = await repository.GetVisibleByOrganizationAndIdAsync(first.Id, secondCustom.Id, CancellationToken.None);
        var platform = firstList.Items.Single(permission => permission.Key == "pg.permissions.view");
        var platformMutationLookup = await repository.GetOrganizationCustomByIdAsync(first.Id, platform.Id, CancellationToken.None);

        firstList.Items.Should().Contain(permission => permission.Id == firstCustom.Id);
        firstList.Items.Should().NotContain(permission => permission.Id == secondCustom.Id);
        crossTenantGet.Should().BeNull();
        platformMutationLookup.Should().BeNull();
    }

    [Fact]
    public async Task DatabaseConstraints_RejectInvalidPermissionTypeScopeAndTenantOwnershipCombinations()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-constraints@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Permission Constraint Org");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();

        await FluentActions.Invoking(() => dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "PermissionDefinitions" (
                    "Id", "OrganizationId", "Key", "NormalizedKey", "DisplayName", "Description", "Module",
                    "PermissionType", "AllowedScopes", "IsRequestable", "IsActive", "CreatedAtUtc", "UpdatedAtUtc", "ArchivedAtUtc", "Version")
                VALUES ({0}, {1}, 'pg.invalid.custom', 'pg.invalid.custom', 'Invalid custom', NULL, 'Permissions',
                    'Custom', 'Organization', false, true, now(), now(), NULL, 1)
                """,
                Guid.NewGuid(),
                organization.Id))
            .Should()
            .ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task PermissionMutations_PersistVersionPolicyVersionAuditAndLifecycle()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-mutate@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Permission Mutation Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var updatePermission = scope.ServiceProvider.GetRequiredService<UpdateCustomPermissionHandler>();
        var archivePermission = scope.ServiceProvider.GetRequiredService<ArchiveCustomPermissionHandler>();
        var activatePermission = scope.ServiceProvider.GetRequiredService<ActivateCustomPermissionHandler>();
        var created = await createPermission.HandleAsync(CreateCommand(organization.Id, "billing.invoice.view"), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var policyVersionAfterCreate = (await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id)).PolicyVersion;

        var updated = await updatePermission.HandleAsync(
            new UpdateCustomPermissionCommand(organization.Id, created.Id, "Billing invoices", "Updated.", "Billing", false),
            CancellationToken.None);
        await archivePermission.HandleAsync(new ArchiveCustomPermissionCommand(organization.Id, created.Id), CancellationToken.None);
        await activatePermission.HandleAsync(new ActivateCustomPermissionCommand(organization.Id, created.Id), CancellationToken.None);

        var persisted = await dbContext.PermissionDefinitions.AsNoTracking().SingleAsync(item => item.Id == created.Id);
        var organizationAfterMutations = await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id);
        updated.Version.Should().BeGreaterThan(created.Version);
        persisted.IsActive.Should().BeTrue();
        persisted.ArchivedAtUtc.Should().BeNull();
        organizationAfterMutations.PolicyVersion.Should().Be(policyVersionAfterCreate + 3);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action.StartsWith("permission."))).Should().Be(4);
    }

    [Fact]
    public async Task MetadataOnlyUpdate_DoesNotIncrementPolicyVersion()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-metadata@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Permission Metadata Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var updatePermission = scope.ServiceProvider.GetRequiredService<UpdateCustomPermissionHandler>();
        var created = await createPermission.HandleAsync(CreateCommand(organization.Id, "billing.invoice.view"), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var before = (await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id)).PolicyVersion;

        await updatePermission.HandleAsync(
            new UpdateCustomPermissionCommand(organization.Id, created.Id, "Billing invoice read", "Metadata only.", "Billing", true),
            CancellationToken.None);

        var after = (await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id)).PolicyVersion;
        after.Should().Be(before);
    }

    [Fact]
    public async Task AuditFailure_RollsBackPermissionAndPolicyVersion()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-rollback@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        long before;
        using (var scope = provider.CreateScope())
        {
            var organization = await CreateOrganizationAsync(scope, "Permission Rollback Org");
            organizationId = organization.Id;
            var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
            before = (await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organizationId)).PolicyVersion;
        }

        await using var failingProvider = await CreateProviderAsync(services =>
            services.AddScoped<IAuditWriter, ThrowingAuditWriter>());
        SetCurrentUser(failingProvider, owner.Id);

        using var failingScope = failingProvider.CreateScope();
        var createPermission = failingScope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();

        await FluentActions.Invoking(() => createPermission.HandleAsync(CreateCommand(organizationId, "billing.invoice.view"), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failure");

        var failingDbContext = failingScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await failingDbContext.PermissionDefinitions.CountAsync(permission => permission.OrganizationId == organizationId)).Should().Be(0);
        var after = (await failingDbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organizationId)).PolicyVersion;
        after.Should().Be(before);
    }

    [Fact]
    public async Task PermissionVersion_ProducesConcurrencyConflict()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "permission-owner-concurrency@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        Guid permissionId;
        using (var scope = provider.CreateScope())
        {
            var organization = await CreateOrganizationAsync(scope, "Permission Concurrency Org");
            var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
            var created = await createPermission.HandleAsync(CreateCommand(organization.Id, "billing.invoice.view"), CancellationToken.None);
            organizationId = organization.Id;
            permissionId = created.Id;
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var first = await firstContext.PermissionDefinitions.SingleAsync(item => item.OrganizationId == organizationId && item.Id == permissionId);
        var second = await secondContext.PermissionDefinitions.SingleAsync(item => item.OrganizationId == organizationId && item.Id == permissionId);

        first.UpdateMetadata("First update", null, "Billing", isRequestable: true, DateTimeOffset.UtcNow);
        second.UpdateMetadata("Second update", null, "Billing", isRequestable: true, DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync();

        await FluentActions.Invoking(() => secondContext.SaveChangesAsync())
            .Should()
            .ThrowAsync<DbUpdateConcurrencyException>();
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

    private static CreateCustomPermissionCommand CreateCommand(Guid organizationId, string key)
    {
        return new CreateCustomPermissionCommand(
            organizationId,
            key,
            "Billing invoice read",
            "Allows billing invoice read access.",
            "Billing",
            PermissionAllowedScopes.Organization,
            IsRequestable: true);
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

    private static async Task<OrganizationResult> CreateOrganizationAsync(IServiceScope scope, string name)
    {
        var createOrganization = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        return await createOrganization.HandleAsync(new CreateOrganizationCommand(name, null), CancellationToken.None);
    }

    private static async Task<List<string>> RolePermissionKeysAsync(
        PermissionGraphDbContext dbContext,
        Guid organizationId,
        string normalizedRoleName,
        string scopeType)
    {
        return await (
            from rolePermission in dbContext.RolePermissions
            join role in dbContext.Roles on rolePermission.RoleId equals role.Id
            join permission in dbContext.PermissionDefinitions on rolePermission.PermissionId equals permission.Id
            where role.OrganizationId == organizationId &&
                  role.NormalizedName == normalizedRoleName &&
                  role.ScopeType == Enum.Parse<RoleScopeType>(scopeType)
            select permission.Key)
            .ToListAsync();
    }

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("audit failure");
        }
    }
}
