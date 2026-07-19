using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Audit;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Common.Errors;
using PermissionGraph.Application.DependencyInjection;
using PermissionGraph.Application.Features.Memberships;
using PermissionGraph.Application.Features.Organizations;
using PermissionGraph.Domain.Memberships;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.Data;
using PermissionGraph.Infrastructure.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace PermissionGraph.IntegrationTests;

public sealed class OrganizationInfrastructureTests : IAsyncLifetime
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
    public async Task CreateOrganization_PersistsOwnerMembershipSeedAndAuditAtomically()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-create@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();

        var result = await handler.HandleAsync(new CreateOrganizationCommand("Acme Engineering", "Platform"), CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Organizations.CountAsync()).Should().Be(1);
        (await dbContext.OrganizationMemberships.CountAsync()).Should().Be(1);
        (await dbContext.OrganizationMemberships.SingleAsync()).UserId.Should().Be(owner.Id);
        (await dbContext.PermissionDefinitions.CountAsync()).Should().BeGreaterThan(0);
        (await dbContext.Roles.CountAsync(role => role.OrganizationId == result.Id)).Should().Be(2);
        (await dbContext.RolePermissions.CountAsync()).Should().BeGreaterThan(0);
        (await dbContext.AuditLogs.CountAsync(audit => audit.Action == "organization.created")).Should().Be(1);
    }

    [Fact]
    public async Task CreateOrganization_RollsBackWhenSeedFails()
    {
        await using var provider = await CreateProviderAsync(services =>
            services.AddScoped<IOrganizationSeedService, ThrowingOrganizationSeedService>());
        var owner = await CreateUserAsync(provider, "owner-seed-fail@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();

        var failedCreate = () => create.HandleAsync(new CreateOrganizationCommand("Seed Failure Org", null), CancellationToken.None);

        await failedCreate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("seed failure");
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.Organizations.CountAsync()).Should().Be(0);
        (await dbContext.OrganizationMemberships.CountAsync()).Should().Be(0);
        (await dbContext.PermissionDefinitions.CountAsync()).Should().Be(0);
        (await dbContext.Roles.CountAsync()).Should().Be(0);
        (await dbContext.RolePermissions.CountAsync()).Should().Be(0);
        (await dbContext.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UniqueOrganizationMembershipConstraint_PreventsDuplicateMembership()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-unique@example.test", isActive: true);
        await CreateUserAsync(provider, "member-unique@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var addMember = scope.ServiceProvider.GetRequiredService<AddOrganizationMemberHandler>();
        var organization = await create.HandleAsync(new CreateOrganizationCommand("Unique Org", null), CancellationToken.None);

        await addMember.HandleAsync(new AddOrganizationMemberCommand(organization.Id, "member-unique@example.test"), CancellationToken.None);
        var duplicate = () => addMember.HandleAsync(new AddOrganizationMemberCommand(organization.Id, "member-unique@example.test"), CancellationToken.None);

        await duplicate.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "membership_already_exists");
    }

    [Fact]
    public async Task AddOrganizationMember_RollsBackWhenAuditFails()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-audit-fail@example.test", isActive: true);
        var member = await CreateUserAsync(provider, "member-audit-fail@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        using (var scope = provider.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
            var organization = await create.HandleAsync(new CreateOrganizationCommand("Audit Failure Org", null), CancellationToken.None);
            organizationId = organization.Id;
        }

        await using var failingProvider = await CreateProviderAsync(services =>
            services.AddScoped<IAuditWriter, ThrowingAuditWriter>());
        SetCurrentUser(failingProvider, owner.Id);

        using var failingScope = failingProvider.CreateScope();
        var addMember = failingScope.ServiceProvider.GetRequiredService<AddOrganizationMemberHandler>();
        var failedAdd = () => addMember.HandleAsync(new AddOrganizationMemberCommand(organizationId, member.Email!), CancellationToken.None);

        await failedAdd.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit failure");
        var dbContext = failingScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        (await dbContext.OrganizationMemberships.CountAsync(item => item.OrganizationId == organizationId && item.UserId == member.Id)).Should().Be(0);
    }

    [Fact]
    public async Task OrganizationScopedMemberLookup_DoesNotReturnSameUserFromAnotherOrganization()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-scope@example.test", isActive: true);
        var member = await CreateUserAsync(provider, "member-scope@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var addMember = scope.ServiceProvider.GetRequiredService<AddOrganizationMemberHandler>();
        var getMember = scope.ServiceProvider.GetRequiredService<GetOrganizationMemberHandler>();
        var first = await create.HandleAsync(new CreateOrganizationCommand("First Org", null), CancellationToken.None);
        var second = await create.HandleAsync(new CreateOrganizationCommand("Second Org", null), CancellationToken.None);
        await addMember.HandleAsync(new AddOrganizationMemberCommand(first.Id, member.Email!), CancellationToken.None);

        var crossTenant = () => getMember.HandleAsync(new GetOrganizationMemberQuery(second.Id, member.Id), CancellationToken.None);

        await crossTenant.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task MembershipLifecycle_PersistsStatusAndAuthorizationVersion()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-life@example.test", isActive: true);
        var member = await CreateUserAsync(provider, "member-life@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var addMember = scope.ServiceProvider.GetRequiredService<AddOrganizationMemberHandler>();
        var suspend = scope.ServiceProvider.GetRequiredService<SuspendOrganizationMemberHandler>();
        var reactivate = scope.ServiceProvider.GetRequiredService<ReactivateOrganizationMemberHandler>();
        var remove = scope.ServiceProvider.GetRequiredService<RemoveOrganizationMemberHandler>();
        var organization = await create.HandleAsync(new CreateOrganizationCommand("Lifecycle Org", null), CancellationToken.None);
        await addMember.HandleAsync(new AddOrganizationMemberCommand(organization.Id, member.Email!), CancellationToken.None);

        await suspend.HandleAsync(new SuspendOrganizationMemberCommand(organization.Id, member.Id), CancellationToken.None);
        await reactivate.HandleAsync(new ReactivateOrganizationMemberCommand(organization.Id, member.Id), CancellationToken.None);
        await remove.HandleAsync(new RemoveOrganizationMemberCommand(organization.Id, member.Id), CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var persisted = await dbContext.OrganizationMemberships.AsNoTracking().SingleAsync(item => item.OrganizationId == organization.Id && item.UserId == member.Id);
        persisted.Status.Should().Be(MembershipStatus.Removed);
        persisted.AuthorizationVersion.Should().Be(4);
    }

    [Fact]
    public async Task ArchiveOrganization_PersistsArchivedStatusAndAudit()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-archive@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var archive = scope.ServiceProvider.GetRequiredService<ArchiveOrganizationHandler>();
        var organization = await create.HandleAsync(new CreateOrganizationCommand("Archive Org", null), CancellationToken.None);

        await archive.HandleAsync(new ArchiveOrganizationCommand(organization.Id, "ARCHIVE"), CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var persisted = await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id);
        persisted.Status.Should().Be(OrganizationStatus.Archived);
        (await dbContext.AuditLogs.CountAsync(item => item.Action == "organization.archived")).Should().Be(1);
    }

    [Fact]
    public async Task TransferOwnership_PersistsOwnerAuditAndAuthorizationVersionIncrements()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-transfer@example.test", isActive: true);
        var member = await CreateUserAsync(provider, "member-transfer@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var addMember = scope.ServiceProvider.GetRequiredService<AddOrganizationMemberHandler>();
        var transfer = scope.ServiceProvider.GetRequiredService<TransferOwnershipHandler>();
        var organization = await create.HandleAsync(new CreateOrganizationCommand("Transfer Org", null), CancellationToken.None);
        await addMember.HandleAsync(new AddOrganizationMemberCommand(organization.Id, member.Email!), CancellationToken.None);

        await transfer.HandleAsync(new TransferOwnershipCommand(organization.Id, member.Id, "ValidPassword123!"), CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var persistedOrganization = await dbContext.Organizations.AsNoTracking().SingleAsync(item => item.Id == organization.Id);
        persistedOrganization.OwnerUserId.Should().Be(member.Id);
        var memberships = await dbContext.OrganizationMemberships.AsNoTracking().Where(item => item.OrganizationId == organization.Id).ToListAsync();
        memberships.Should().Contain(item => item.UserId == owner.Id && item.AuthorizationVersion == 2);
        memberships.Should().Contain(item => item.UserId == member.Id && item.AuthorizationVersion == 2);
        (await dbContext.AuditLogs.CountAsync(item => item.Action == "organization.ownership_transferred")).Should().Be(1);
    }

    [Fact]
    public async Task RecentAuthenticationVerifier_UsesCurrentPasswordAndActiveAccount()
    {
        await using var provider = await CreateProviderAsync();
        var user = await CreateUserAsync(provider, "recent@example.test", isActive: true);
        var inactive = await CreateUserAsync(provider, "recent-inactive@example.test", isActive: false);

        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<PermissionGraph.Application.Abstractions.Security.IRecentAuthenticationVerifier>();

        (await verifier.HasRecentAuthenticationAsync(user.Id, "ValidPassword123!", CancellationToken.None)).Should().BeTrue();
        (await verifier.HasRecentAuthenticationAsync(user.Id, "WrongPassword123!", CancellationToken.None)).Should().BeFalse();
        (await verifier.HasRecentAuthenticationAsync(inactive.Id, "ValidPassword123!", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task UserAccountLookup_ReturnsActiveAndInactiveIdentityUsers()
    {
        await using var provider = await CreateProviderAsync();
        var active = await CreateUserAsync(provider, "lookup-active@example.test", isActive: true);
        var inactive = await CreateUserAsync(provider, "lookup-inactive@example.test", isActive: false);

        using var scope = provider.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IUserAccountLookup>();

        (await lookup.FindByIdAsync(active.Id, CancellationToken.None)).Should().BeEquivalentTo(new UserAccount(active.Id, active.Email!, active.DisplayName, true));
        (await lookup.FindByEmailAsync(inactive.Email!, CancellationToken.None)).Should().BeEquivalentTo(new UserAccount(inactive.Id, inactive.Email!, inactive.DisplayName, false));
        (await lookup.FindByEmailAsync("missing@example.test", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task OptimisticConcurrency_ProducesConflict()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-concurrency@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        Guid organizationId;
        using (var scope = provider.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
            var organization = await create.HandleAsync(new CreateOrganizationCommand("Concurrency Org", null), CancellationToken.None);
            organizationId = organization.Id;
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        var first = await firstContext.Organizations.SingleAsync(item => item.Id == organizationId);
        var second = await secondContext.Organizations.SingleAsync(item => item.Id == organizationId);

        first.UpdateDetails("First Update", "FIRST UPDATE", null, DateTimeOffset.UtcNow);
        second.UpdateDetails("Second Update", "SECOND UPDATE", null, DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync();

        var concurrentSave = () => secondContext.SaveChangesAsync();

        await concurrentSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task RestrictiveDelete_PreventsDeletingOrganizationWithMembership()
    {
        await using var provider = await CreateProviderAsync();
        var owner = await CreateUserAsync(provider, "owner-delete@example.test", isActive: true);
        SetCurrentUser(provider, owner.Id);

        using var scope = provider.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateOrganizationHandler>();
        var organization = await create.HandleAsync(new CreateOrganizationCommand("Restrict Org", null), CancellationToken.None);
        var dbContext = scope.ServiceProvider.GetRequiredService<PermissionGraphDbContext>();
        dbContext.ChangeTracker.Clear();
        dbContext.Organizations.Remove(await dbContext.Organizations.SingleAsync(item => item.Id == organization.Id));

        var delete = () => dbContext.SaveChangesAsync();

        await delete.Should().ThrowAsync<DbUpdateException>();
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

    private sealed class MutableCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }

    private sealed class ThrowingOrganizationSeedService : IOrganizationSeedService
    {
        public Task SeedDefaultAuthorizationAsync(Organization organization, Guid actorUserId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("seed failure");
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
