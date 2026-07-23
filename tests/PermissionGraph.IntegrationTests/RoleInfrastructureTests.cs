namespace PermissionGraph.IntegrationTests;

public sealed class RoleInfrastructureTests : IAsyncLifetime
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
    public async Task SystemRoleSeed_ContainsFiveRolesIsIdempotentAndRemovesSensitiveAdministratorMappings()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "role-owner-seed@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Role Seed Org");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<IOrganizationSeedService>();

        await seed.SeedDefaultAuthorizationAsync(
            await dbContext.Organizations.SingleAsync(item => item.Id == organization.Id),
            owner.Id,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var systemRoles = await dbContext.Roles
            .Where(role => role.OrganizationId == organization.Id && role.RoleType == RoleType.System)
            .Select(role => new { role.Id, role.NormalizedName, role.ScopeType })
            .ToListAsync();
        var duplicateMappings = await dbContext.RolePermissions
            .GroupBy(mapping => new { mapping.RoleId, mapping.PermissionId })
            .Where(group => group.Count() > 1)
            .CountAsync();
        var administratorPermissions = await RolePermissionKeysAsync(dbContext, organization.Id, "ORGANIZATION ADMINISTRATOR", RoleScopeType.Organization);

        systemRoles.Select(role => role.NormalizedName).Should().BeEquivalentTo(
            "ORGANIZATION ADMINISTRATOR",
            "ORGANIZATION MEMBER",
            "PROJECT ADMINISTRATOR",
            "PROJECT CONTRIBUTOR",
            "PROJECT VIEWER");
        systemRoles.Select(role => role.Id).Should().OnlyHaveUniqueItems();
        duplicateMappings.Should().Be(0);
        administratorPermissions.Should().NotContain("pg.organizations.archive");
        administratorPermissions.Should().NotContain("pg.organizations.transfer_ownership");
    }

    [Fact]
    public async Task RoleRepository_UsesOrganizationScopedVisibilityAndActiveNameUniqueness()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "role-owner-repository@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Role Repository Org");
        var second = await CreateOrganizationAsync(scope, "Second Role Repository Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var createRole = scope.ServiceProvider.GetRequiredService<CreateCustomRoleHandler>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var firstPermission = await createPermission.HandleAsync(PermissionCommand(first.Id, "documents.review"), CancellationToken.None);
        var secondPermission = await createPermission.HandleAsync(PermissionCommand(second.Id, "documents.review"), CancellationToken.None);
        var firstRole = await createRole.HandleAsync(new CreateCustomRoleCommand(first.Id, "Document Reviewer", null, RoleScopeType.Organization, true, [firstPermission.Id]), CancellationToken.None);
        var secondRole = await createRole.HandleAsync(new CreateCustomRoleCommand(second.Id, "Document Reviewer", null, RoleScopeType.Organization, true, [secondPermission.Id]), CancellationToken.None);

        var mismatch = await repository.GetVisibleByOrganizationAndIdAsync(first.Id, secondRole.Id, CancellationToken.None);
        var list = await repository.ListVisibleForOrganizationAsync(first.Id, new RoleListFilters(null, null, null, null, null), page: 1, pageSize: 100, CancellationToken.None);
        var duplicateInFirst = await repository.ActiveNormalizedNameExistsAsync(first.Id, RoleScopeType.Organization, "DOCUMENT REVIEWER", excludingRoleId: null, CancellationToken.None);
        var sameNameDifferentScope = await repository.ActiveNormalizedNameExistsAsync(first.Id, RoleScopeType.Project, "DOCUMENT REVIEWER", excludingRoleId: null, CancellationToken.None);

        mismatch.Should().BeNull();
        list.Items.Should().ContainSingle(role => role.Id == firstRole.Id);
        duplicateInFirst.Should().BeTrue();
        sameNameDifferentScope.Should().BeFalse();
    }

    [Fact]
    public async Task RoleMutations_PersistChildrenVersionPolicyVersionAuditAndLifecycle()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "role-owner-mutation@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var organization = await CreateOrganizationAsync(scope, "Role Mutation Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var createRole = scope.ServiceProvider.GetRequiredService<CreateCustomRoleHandler>();
        var updateRole = scope.ServiceProvider.GetRequiredService<UpdateCustomRoleHandler>();
        var replacePermissions = scope.ServiceProvider.GetRequiredService<ReplaceRolePermissionsHandler>();
        var archiveRole = scope.ServiceProvider.GetRequiredService<ArchiveCustomRoleHandler>();
        var activateRole = scope.ServiceProvider.GetRequiredService<ActivateCustomRoleHandler>();
        var permission = await createPermission.HandleAsync(PermissionCommand(organization.Id, "documents.review"), CancellationToken.None);
        var replacement = await createPermission.HandleAsync(PermissionCommand(organization.Id, "documents.approve"), CancellationToken.None);
        var created = await createRole.HandleAsync(new CreateCustomRoleCommand(organization.Id, "Document Reviewer", null, RoleScopeType.Organization, true, [permission.Id]), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var policyAfterCreate = (await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id)).PolicyVersion;

        var updated = await updateRole.HandleAsync(new UpdateCustomRoleCommand(organization.Id, created.Id, "Document Approver", "Updated.", false), CancellationToken.None);
        await replacePermissions.HandleAsync(new ReplaceRolePermissionsCommand(organization.Id, created.Id, [replacement.Id]), CancellationToken.None);
        await archiveRole.HandleAsync(new ArchiveCustomRoleCommand(organization.Id, created.Id), CancellationToken.None);
        await activateRole.HandleAsync(new ActivateCustomRoleCommand(organization.Id, created.Id), CancellationToken.None);

        var persisted = await dbContext.Roles.AsNoTracking().Include(role => role.Permissions).SingleAsync(role => role.Id == created.Id);
        var organizationAfterMutations = await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id);
        updated.Version.Should().BeGreaterThan(created.Version);
        persisted.IsActive.Should().BeTrue();
        persisted.ArchivedAtUtc.Should().BeNull();
        persisted.Permissions.Should().ContainSingle(mapping => mapping.PermissionId == replacement.Id);
        organizationAfterMutations.PolicyVersion.Should().Be(policyAfterCreate + 4);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action.StartsWith("role."))).Should().Be(5);
    }

    [Fact]
    public async Task RoleDatabaseConstraints_RejectInvalidValuesDuplicatesAndCrossTenantMappings()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "role-owner-constraints@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var first = await CreateOrganizationAsync(scope, "First Role Constraint Org");
        var second = await CreateOrganizationAsync(scope, "Second Role Constraint Org");
        var createPermission = scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>();
        var createRole = scope.ServiceProvider.GetRequiredService<CreateCustomRoleHandler>();
        var firstPermission = await createPermission.HandleAsync(PermissionCommand(first.Id, "documents.review"), CancellationToken.None);
        var secondPermission = await createPermission.HandleAsync(PermissionCommand(second.Id, "documents.review"), CancellationToken.None);
        var role = await createRole.HandleAsync(new CreateCustomRoleCommand(first.Id, "Document Reviewer", null, RoleScopeType.Organization, true, [firstPermission.Id]), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();

        await FluentActions.Invoking(() => dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Roles" (
                    "Id", "OrganizationId", "Name", "NormalizedName", "Description", "ScopeType", "RoleType",
                    "IsRequestable", "IsActive", "CreatedAtUtc", "UpdatedAtUtc", "ArchivedAtUtc", "Version")
                VALUES ({0}, {1}, 'Invalid', 'INVALID', NULL, 'BadScope', 'Custom', true, true, now(), now(), NULL, 1)
                """,
                Guid.NewGuid(),
                first.Id))
            .Should()
            .ThrowAsync<PostgresException>();

        await FluentActions.Invoking(() => dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "AddedAtUtc", "AddedByUserId")
                VALUES ({0}, {1}, now(), {2})
                """,
                role.Id,
                firstPermission.Id,
                owner.Id))
            .Should()
            .ThrowAsync<PostgresException>();

        await FluentActions.Invoking(() => dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "AddedAtUtc", "AddedByUserId")
                VALUES ({0}, {1}, now(), {2})
                """,
                role.Id,
                secondPermission.Id,
                owner.Id))
            .Should()
            .ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task RoleVersion_ProducesConcurrencyConflict()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "role-owner-concurrency@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        Guid roleId;
        using (var scope = provider.CreateScope())
        {
            var organization = await CreateOrganizationAsync(scope, "Role Concurrency Org");
            var permission = await scope.ServiceProvider.GetRequiredService<CreateCustomPermissionHandler>()
                .HandleAsync(PermissionCommand(organization.Id, "documents.review"), CancellationToken.None);
            var role = await scope.ServiceProvider.GetRequiredService<CreateCustomRoleHandler>()
                .HandleAsync(new CreateCustomRoleCommand(organization.Id, "Document Reviewer", null, RoleScopeType.Organization, true, [permission.Id]), CancellationToken.None);
            organizationId = organization.Id;
            roleId = role.Id;
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var first = await firstContext.Roles.SingleAsync(role => role.OrganizationId == organizationId && role.Id == roleId);
        var second = await secondContext.Roles.SingleAsync(role => role.OrganizationId == organizationId && role.Id == roleId);

        first.UpdateMetadata("First Update", "FIRST UPDATE", null, true, DateTimeOffset.UtcNow);
        second.UpdateMetadata("Second Update", "SECOND UPDATE", null, true, DateTimeOffset.UtcNow);
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

    private static CreateCustomPermissionCommand PermissionCommand(Guid organizationId, string key)
    {
        return new CreateCustomPermissionCommand(
            organizationId,
            key,
            "Document permission",
            null,
            "Documents",
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
        RoleScopeType scopeType)
    {
        return await (
            from rolePermission in dbContext.RolePermissions
            join role in dbContext.Roles on rolePermission.RoleId equals role.Id
            join permission in dbContext.PermissionDefinitions on rolePermission.PermissionId equals permission.Id
            where role.OrganizationId == organizationId &&
                  role.NormalizedName == normalizedRoleName &&
                  role.ScopeType == scopeType
            select permission.Key)
            .ToListAsync();
    }

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }
}
