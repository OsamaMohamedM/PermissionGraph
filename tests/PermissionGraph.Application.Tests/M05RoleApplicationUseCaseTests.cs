namespace PermissionGraph.Application.Tests;

public sealed class M05RoleApplicationUseCaseTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CloneRoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccd");
    private static readonly Guid PermissionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ProjectPermissionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid PlatformPermissionId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RoleUseCase_RejectsMissingCurrentUserAndInactiveAccount()
    {
        var missing = RoleUseCaseFixture.Create(null);
        var missingAct = () => missing.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId), CancellationToken.None);
        await missingAct.Should().ThrowAsync<UnauthorizedApplicationException>();

        var inactive = RoleUseCaseFixture.Create(OwnerId);
        inactive.Users.Accounts[OwnerId] = inactive.Users.Accounts[OwnerId] with { IsActive = false };
        var inactiveAct = () => inactive.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId), CancellationToken.None);
        await inactiveAct.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task ActiveMember_CanListAndGetVisibleRoles()
    {
        var fixture = RoleUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        var role = fixture.AddCustomRole();
        fixture.AddCustomRole(Guid.Parse("99999999-9999-9999-9999-999999999999"), OtherOrganizationId, "Other Org Role");

        var list = await fixture.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId), CancellationToken.None);
        var get = await fixture.GetRoleHandler.HandleAsync(new GetRoleQuery(OrganizationId, role.Id), CancellationToken.None);
        var crossTenant = () => fixture.GetRoleHandler.HandleAsync(
            new GetRoleQuery(OrganizationId, Guid.Parse("99999999-9999-9999-9999-999999999999")),
            CancellationToken.None);

        list.Items.Should().ContainSingle(item => item.Id == role.Id);
        get.OrganizationId.Should().Be(OrganizationId);
        await crossTenant.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task NonMemberSuspendedOrRemovedMember_ReturnsSafeNotFound()
    {
        var nonMember = RoleUseCaseFixture.Create(OtherUserId);
        nonMember.AddOrganization();
        nonMember.AddCustomRole();
        var nonMemberAct = () => nonMember.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId), CancellationToken.None);
        await nonMemberAct.Should().ThrowAsync<NotFoundApplicationException>();

        var suspended = RoleUseCaseFixture.Create(MemberId);
        suspended.AddOrganization();
        var suspendedMembership = suspended.AddMembership(MemberId);
        suspendedMembership.Suspend(isOwner: false, Now.AddMinutes(1));
        var suspendedAct = () => suspended.GetRoleHandler.HandleAsync(new GetRoleQuery(OrganizationId, RoleId), CancellationToken.None);
        await suspendedAct.Should().ThrowAsync<NotFoundApplicationException>();

        var removed = RoleUseCaseFixture.Create(MemberId);
        removed.AddOrganization();
        var removedMembership = removed.AddMembership(MemberId);
        removedMembership.Remove(isOwner: false, Now.AddMinutes(1));
        var removedAct = () => removed.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId), CancellationToken.None);
        await removedAct.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task NonOwner_CannotMutateRoles()
    {
        var fixture = RoleUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        fixture.AddCustomPermission();
        fixture.AddCustomRole();

        var create = () => fixture.CreateCustomRoleHandler.HandleAsync(
                new CreateCustomRoleCommand(OrganizationId, "Editors", null, RoleScopeType.Organization, true, [PermissionId]),
                CancellationToken.None);
        var update = () => fixture.UpdateCustomRoleHandler.HandleAsync(
                new UpdateCustomRoleCommand(OrganizationId, RoleId, "Editors", null, true),
                CancellationToken.None);
        var replace = () => fixture.ReplaceRolePermissionsHandler.HandleAsync(
                new ReplaceRolePermissionsCommand(OrganizationId, RoleId, [PermissionId]),
                CancellationToken.None);

        await create.Should().ThrowAsync<ForbiddenApplicationException>();
        await update.Should().ThrowAsync<ForbiddenApplicationException>();
        await replace.Should().ThrowAsync<ForbiddenApplicationException>();
    }

    [Fact]
    public async Task OwnerCreate_AddsRolePolicyVersionAuditAndTransaction()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.GuidProvider.Enqueue(RoleId);
        fixture.AddOrganization();
        fixture.AddCustomPermission();

        var result = await fixture.CreateCustomRoleHandler.HandleAsync(
            new CreateCustomRoleCommand(OrganizationId, "Editors", "Can edit documents.", RoleScopeType.Organization, true, [PermissionId]),
            CancellationToken.None);

        result.Id.Should().Be(RoleId);
        result.NormalizedName.Should().Be("EDITORS");
        result.RoleType.Should().Be(RoleType.Custom);
        result.PermissionIds.Should().ContainSingle(permissionId => permissionId == PermissionId);
        fixture.Roles.Items.Should().ContainSingle(role => role.Id == RoleId);
        fixture.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "role.created");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task Create_RejectsDuplicateActiveName()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddCustomPermission();
        fixture.AddCustomRole(name: "Editors");

        var act = () => fixture.CreateCustomRoleHandler.HandleAsync(
            new CreateCustomRoleCommand(OrganizationId, "editors", null, RoleScopeType.Organization, true, [PermissionId]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_name_already_exists");
    }

    [Fact]
    public async Task UpdateCustomRole_ChangesMetadataAndIncrementsPolicyVersion()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddCustomRole();

        var result = await fixture.UpdateCustomRoleHandler.HandleAsync(
            new UpdateCustomRoleCommand(OrganizationId, RoleId, "Senior Editors", "Updated.", false),
            CancellationToken.None);

        result.Name.Should().Be("Senior Editors");
        result.NormalizedName.Should().Be("SENIOR EDITORS");
        result.IsRequestable.Should().BeFalse();
        fixture.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "role.updated");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task SystemRoleMutation_IsBlockedByDomain()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddSystemRole();

        var update = () => fixture.UpdateCustomRoleHandler.HandleAsync(
            new UpdateCustomRoleCommand(OrganizationId, RoleId, "Rename", null, false),
            CancellationToken.None);
        var archive = () => fixture.ArchiveCustomRoleHandler.HandleAsync(new ArchiveCustomRoleCommand(OrganizationId, RoleId), CancellationToken.None);

        await update.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
        await archive.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
    }

    [Fact]
    public async Task CloneRole_ProducesNewCustomRoleAndCoordinatesPolicyAuditTransaction()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.GuidProvider.Enqueue(CloneRoleId);
        fixture.AddOrganization();
        fixture.AddCustomPermission();
        fixture.AddSystemRole();

        var result = await fixture.CloneRoleHandler.HandleAsync(
            new CloneRoleCommand(OrganizationId, RoleId, "Copied Admin", "Copied.", true),
            CancellationToken.None);

        result.Id.Should().Be(CloneRoleId);
        result.RoleType.Should().Be(RoleType.Custom);
        result.PermissionIds.Should().ContainSingle(permissionId => permissionId == PermissionId);
        fixture.Roles.Items.Should().ContainSingle(role => role.Id == CloneRoleId);
        fixture.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "role.cloned");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task ArchiveAndActivate_CustomRoleLifecycle()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var role = fixture.AddCustomRole();

        await fixture.ArchiveCustomRoleHandler.HandleAsync(new ArchiveCustomRoleCommand(OrganizationId, RoleId), CancellationToken.None);

        role.IsActive.Should().BeFalse();
        fixture.PolicyVersion.Records.Should().Contain(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().Contain(record => record.Action == "role.archived");

        await fixture.ActivateCustomRoleHandler.HandleAsync(new ActivateCustomRoleCommand(OrganizationId, RoleId), CancellationToken.None);

        role.IsActive.Should().BeTrue();
        fixture.PolicyVersion.Records.Should().HaveCount(2);
        fixture.AuditWriter.Records.Should().Contain(record => record.Action == "role.activated");
    }

    [Fact]
    public async Task ReplacePermissions_UpdatesMatrixAndIncrementsPolicyVersion()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddCustomPermission();
        fixture.AddPlatformPermission(PermissionAllowedScopes.OrganizationAndProject);
        var role = fixture.AddCustomRole(permissions: []);

        var result = await fixture.ReplaceRolePermissionsHandler.HandleAsync(
            new ReplaceRolePermissionsCommand(OrganizationId, role.Id, [PermissionId, PlatformPermissionId]),
            CancellationToken.None);

        result.PermissionIds.Should().BeEquivalentTo([PermissionId, PlatformPermissionId]);
        fixture.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "role.permissions_updated");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task ReplacePermissions_RejectsDuplicateInactiveScopeIncompatibleAndCrossTenantPermissions()
    {
        var duplicate = RoleUseCaseFixture.Create(OwnerId);
        duplicate.AddOrganization();
        duplicate.AddCustomPermission();
        duplicate.AddCustomRole();
        var duplicateAct = () => duplicate.ReplaceRolePermissionsHandler.HandleAsync(
                new ReplaceRolePermissionsCommand(OrganizationId, RoleId, [PermissionId, PermissionId]),
                CancellationToken.None);
        await duplicateAct.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_permission_duplicate");

        var inactive = RoleUseCaseFixture.Create(OwnerId);
        inactive.AddOrganization();
        var inactivePermission = inactive.AddCustomPermission();
        inactivePermission.Archive(Now.AddMinutes(1));
        inactive.AddCustomRole(permissions: []);
        var inactiveAct = () => inactive.ReplaceRolePermissionsHandler.HandleAsync(
                new ReplaceRolePermissionsCommand(OrganizationId, RoleId, [PermissionId]),
                CancellationToken.None);
        await inactiveAct.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_permission_inactive");

        var incompatible = RoleUseCaseFixture.Create(OwnerId);
        incompatible.AddOrganization();
        incompatible.AddCustomPermission(allowedScopes: PermissionAllowedScopes.Project);
        incompatible.AddCustomRole(permissions: []);
        var incompatibleAct = () => incompatible.ReplaceRolePermissionsHandler.HandleAsync(
                new ReplaceRolePermissionsCommand(OrganizationId, RoleId, [PermissionId]),
                CancellationToken.None);
        await incompatibleAct.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_permission_scope_incompatible");

        var crossTenant = RoleUseCaseFixture.Create(OwnerId);
        crossTenant.AddOrganization();
        crossTenant.AddCustomPermission(organizationId: OtherOrganizationId);
        crossTenant.AddCustomRole(permissions: []);
        var crossTenantAct = () => crossTenant.ReplaceRolePermissionsHandler.HandleAsync(
                new ReplaceRolePermissionsCommand(OrganizationId, RoleId, [PermissionId]),
                CancellationToken.None);
        await crossTenantAct.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task ReplacePermissions_AcceptsCompatiblePlatformPermissionAndNoOpDoesNotWrite()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddPlatformPermission(PermissionAllowedScopes.OrganizationAndProject);
        var role = fixture.AddCustomRole(permissions: [fixture.Permissions.Items.Single()]);

        var noOp = await fixture.ReplaceRolePermissionsHandler.HandleAsync(
            new ReplaceRolePermissionsCommand(OrganizationId, role.Id, [PlatformPermissionId]),
            CancellationToken.None);

        noOp.PermissionIds.Should().ContainSingle(permissionId => permissionId == PlatformPermissionId);
        fixture.PolicyVersion.Records.Should().BeEmpty();
        fixture.AuditWriter.Records.Should().BeEmpty();
        fixture.Transaction.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuditOrPolicyFailure_PreventsCommit()
    {
        var auditFailure = RoleUseCaseFixture.Create(OwnerId);
        auditFailure.AddOrganization();
        auditFailure.AddCustomPermission();
        auditFailure.AuditWriter.ThrowOnWrite = true;

        var auditFailureAct = () => auditFailure.CreateCustomRoleHandler.HandleAsync(
                new CreateCustomRoleCommand(OrganizationId, "Editors", null, RoleScopeType.Organization, true, [PermissionId]),
                CancellationToken.None);
        await auditFailureAct.Should().ThrowAsync<InvalidOperationException>();
        auditFailure.Transaction.CommitCalls.Should().Be(0);

        var policyFailure = RoleUseCaseFixture.Create(OwnerId);
        policyFailure.AddOrganization();
        policyFailure.AddCustomPermission();
        policyFailure.PolicyVersion.ThrowOnIncrement = true;

        var policyFailureAct = () => policyFailure.CreateCustomRoleHandler.HandleAsync(
                new CreateCustomRoleCommand(OrganizationId, "Editors", null, RoleScopeType.Organization, true, [PermissionId]),
                CancellationToken.None);
        await policyFailureAct.Should().ThrowAsync<InvalidOperationException>();
        policyFailure.Transaction.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task List_ValidatesPaginationAndFilters()
    {
        var fixture = RoleUseCaseFixture.Create(OwnerId);

        var invalidPage = () => fixture.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId, Page: 0), CancellationToken.None);
        var invalidType = () => fixture.ListRolesHandler.HandleAsync(new ListRolesQuery(OrganizationId, RoleType: (RoleType)99), CancellationToken.None);

        await invalidPage.Should().ThrowAsync<CommandValidationException>();
        await invalidType.Should().ThrowAsync<CommandValidationException>();
    }

    private sealed class RoleUseCaseFixture
    {
        private RoleUseCaseFixture(Guid? currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Organizations = new FakeOrganizationRepository();
            Memberships = new FakeOrganizationMembershipRepository();
            Roles = new FakeRoleRepository();
            Permissions = new FakePermissionDefinitionRepository();
            PolicyVersion = new FakeOrganizationPolicyVersionUpdater();
            AuditWriter = new FakeAuditWriter();
            Transaction = new FakeApplicationTransaction();
            GuidProvider = new FakeGuidProvider();
            Clock = new FakeClock(Now);

            if (currentUserId is not null)
            {
                Users.Accounts[currentUserId.Value] = new UserAccount(currentUserId.Value, $"{currentUserId}@example.test", "Current User", IsActive: true);
            }

            var resolver = new AuthenticatedUserResolver(CurrentUser, Users);
            var organizationAccess = new OrganizationAccessHelper(Organizations, Memberships);
            var roleCatalogAccess = new RoleCatalogAccessHelper(organizationAccess, Roles);

            ListRolesHandler = new ListRolesHandler(new ListRolesQueryValidator(), resolver, roleCatalogAccess, Roles);
            GetRoleHandler = new GetRoleHandler(new GetRoleQueryValidator(), resolver, roleCatalogAccess);
            CreateCustomRoleHandler = new CreateCustomRoleHandler(
                new CreateCustomRoleCommandValidator(),
                resolver,
                roleCatalogAccess,
                Permissions,
                Roles,
                PolicyVersion,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            UpdateCustomRoleHandler = new UpdateCustomRoleHandler(
                new UpdateCustomRoleCommandValidator(),
                resolver,
                roleCatalogAccess,
                Roles,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
            CloneRoleHandler = new CloneRoleHandler(
                new CloneRoleCommandValidator(),
                resolver,
                roleCatalogAccess,
                Roles,
                PolicyVersion,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            ArchiveCustomRoleHandler = new ArchiveCustomRoleHandler(
                new ArchiveCustomRoleCommandValidator(),
                resolver,
                roleCatalogAccess,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
            ActivateCustomRoleHandler = new ActivateCustomRoleHandler(
                new ActivateCustomRoleCommandValidator(),
                resolver,
                roleCatalogAccess,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
            ReplaceRolePermissionsHandler = new ReplaceRolePermissionsHandler(
                new ReplaceRolePermissionsCommandValidator(),
                resolver,
                roleCatalogAccess,
                Permissions,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeOrganizationRepository Organizations { get; }
        public FakeOrganizationMembershipRepository Memberships { get; }
        public FakeRoleRepository Roles { get; }
        public FakePermissionDefinitionRepository Permissions { get; }
        public FakeOrganizationPolicyVersionUpdater PolicyVersion { get; }
        public FakeAuditWriter AuditWriter { get; }
        public FakeApplicationTransaction Transaction { get; }
        public FakeGuidProvider GuidProvider { get; }
        public FakeClock Clock { get; }
        public ListRolesHandler ListRolesHandler { get; }
        public GetRoleHandler GetRoleHandler { get; }
        public CreateCustomRoleHandler CreateCustomRoleHandler { get; }
        public UpdateCustomRoleHandler UpdateCustomRoleHandler { get; }
        public CloneRoleHandler CloneRoleHandler { get; }
        public ArchiveCustomRoleHandler ArchiveCustomRoleHandler { get; }
        public ActivateCustomRoleHandler ActivateCustomRoleHandler { get; }
        public ReplaceRolePermissionsHandler ReplaceRolePermissionsHandler { get; }

        public static RoleUseCaseFixture Create(Guid? currentUserId)
        {
            return new RoleUseCaseFixture(currentUserId);
        }

        public Organization AddOrganization(Guid? organizationId = null, Guid? ownerUserId = null)
        {
            var organization = Organization.Create(
                organizationId ?? OrganizationId,
                "Acme Engineering",
                "ACME ENGINEERING",
                null,
                ownerUserId ?? OwnerId,
                Now);
            Organizations.Items.Add(organization);

            if ((ownerUserId ?? OwnerId) == CurrentUser.UserId)
            {
                AddMembership(ownerUserId ?? OwnerId, organization.Id);
            }

            return organization;
        }

        public OrganizationMembership AddMembership(Guid userId, Guid? organizationId = null)
        {
            var membership = OrganizationMembership.CreateActive(Guid.NewGuid(), organizationId ?? OrganizationId, userId, Now, Now);
            Memberships.Items.Add(membership);
            return membership;
        }

        public PermissionDefinition AddCustomPermission(
            Guid? permissionId = null,
            Guid? organizationId = null,
            PermissionAllowedScopes allowedScopes = PermissionAllowedScopes.Organization)
        {
            var permission = PermissionDefinition.CreateCustom(
                permissionId ?? PermissionId,
                organizationId ?? OrganizationId,
                $"documents.{(permissionId ?? PermissionId).ToString()[..8]}",
                $"documents.{(permissionId ?? PermissionId).ToString()[..8]}",
                "Manage documents",
                null,
                "Documents",
                allowedScopes,
                true,
                Now);
            Permissions.Items.Add(permission);
            return permission;
        }

        public PermissionDefinition AddPlatformPermission(PermissionAllowedScopes allowedScopes)
        {
            var permission = PermissionDefinition.CreatePlatform(
                PlatformPermissionId,
                "pg.roles.view",
                "pg.roles.view",
                "View roles",
                null,
                "Roles",
                allowedScopes,
                false,
                Now);
            Permissions.Items.Add(permission);
            return permission;
        }

        public Role AddCustomRole(
            Guid? roleId = null,
            Guid? organizationId = null,
            string name = "Editors",
            IReadOnlyCollection<PermissionDefinition>? permissions = null)
        {
            var role = Role.CreateCustom(
                roleId ?? RoleId,
                organizationId ?? OrganizationId,
                name,
                name.ToUpperInvariant(),
                null,
                RoleScopeType.Organization,
                true,
                Now,
                permissions ?? [],
                OwnerId);
            Roles.Items.Add(role);
            return role;
        }

        public Role AddSystemRole()
        {
            var permission = Permissions.Items.SingleOrDefault(item => item.Id == PermissionId) ?? AddCustomPermission();
            var role = Role.CreateSystem(
                RoleId,
                OrganizationId,
                "Organization Administrator",
                "ORGANIZATION ADMINISTRATOR",
                null,
                RoleScopeType.Organization,
                false,
                Now,
                [permission],
                OwnerId);
            Roles.Items.Add(role);
            return role;
        }
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class FakeUserAccountLookup : IUserAccountLookup
    {
        public Dictionary<Guid, UserAccount> Accounts { get; } = [];

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            Accounts.TryGetValue(userId, out var account);
            return Task.FromResult(account);
        }

        public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(Accounts.Values.SingleOrDefault(account => account.Email == email));
        }
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public List<Organization> Items { get; } = [];

        public Task AddAsync(Organization organization, CancellationToken cancellationToken)
        {
            Items.Add(organization);
            return Task.CompletedTask;
        }

        public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == organizationId));
        }

        public Task<PagedResult<Organization>> ListForUserAsync(Guid userId, int pageSize, string? cursor, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PagedResult<Organization>([], null));
        }
    }

    private sealed class FakeOrganizationMembershipRepository : IOrganizationMembershipRepository
    {
        public List<OrganizationMembership> Items { get; } = [];

        public Task AddAsync(OrganizationMembership membership, CancellationToken cancellationToken)
        {
            Items.Add(membership);
            return Task.CompletedTask;
        }

        public Task<OrganizationMembership?> GetByOrganizationAndUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.UserId == userId && item.Status != MembershipStatus.Removed));
        }

        public Task<OrganizationMembership?> GetByOrganizationAndUserIncludingRemovedAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.UserId == userId));
        }

        public Task<PagedResult<OrganizationMemberResult>> ListMembersAsync(Guid organizationId, int pageSize, string? cursor, string? search, string? status, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PagedResult<OrganizationMemberResult>([], null));
        }

        public Task<OrganizationMemberResult?> GetMemberResultAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<OrganizationMemberResult?>(null);
        }

        public Task IncrementAuthorizationVersionAsync(Guid organizationId, Guid userId, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public List<Role> Items { get; } = [];

        public Task AddAsync(Role role, CancellationToken cancellationToken)
        {
            Items.Add(role);
            return Task.CompletedTask;
        }

        public Task<Role?> GetVisibleByOrganizationAndIdAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.Id == roleId));
        }

        public Task<PageResult<Role>> ListVisibleForOrganizationAsync(Guid organizationId, RoleListFilters filters, int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = Items.Where(item => item.OrganizationId == organizationId);
            if (filters.RoleType is not null)
            {
                query = query.Where(item => item.RoleType == filters.RoleType);
            }

            if (filters.ScopeType is not null)
            {
                query = query.Where(item => item.ScopeType == filters.ScopeType);
            }

            if (filters.IsActive is not null)
            {
                query = query.Where(item => item.IsActive == filters.IsActive);
            }

            if (filters.IsRequestable is not null)
            {
                query = query.Where(item => item.IsRequestable == filters.IsRequestable);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                query = query.Where(item => item.Name.Contains(filters.Search, StringComparison.OrdinalIgnoreCase));
            }

            var all = query.ToArray();
            return Task.FromResult(new PageResult<Role>(all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, all.Length));
        }

        public Task<bool> ActiveNormalizedNameExistsAsync(Guid organizationId, RoleScopeType scopeType, string normalizedName, Guid? excludingRoleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Any(item =>
                item.OrganizationId == organizationId &&
                item.ScopeType == scopeType &&
                item.IsActive &&
                item.Id != excludingRoleId &&
                string.Equals(item.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private sealed class FakePermissionDefinitionRepository : IPermissionDefinitionRepository
    {
        public List<PermissionDefinition> Items { get; } = [];

        public Task AddAsync(PermissionDefinition permission, CancellationToken cancellationToken)
        {
            Items.Add(permission);
            return Task.CompletedTask;
        }

        public Task<PermissionDefinition?> GetVisibleByOrganizationAndIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.Id == permissionId &&
                (item.PermissionType == PermissionType.Platform || item.OrganizationId == organizationId)));
        }

        public Task<PermissionDefinition?> GetOrganizationCustomByIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == permissionId && item.PermissionType == PermissionType.Custom && item.OrganizationId == organizationId));
        }

        public Task<PageResult<PermissionDefinition>> ListVisibleForOrganizationAsync(Guid organizationId, PermissionDefinitionListFilters filters, int page, int pageSize, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PageResult<PermissionDefinition>([], page, pageSize, 0));
        }

        public Task<bool> CustomNormalizedKeyExistsAsync(Guid organizationId, string normalizedKey, Guid? excludingPermissionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeOrganizationPolicyVersionUpdater : IOrganizationPolicyVersionUpdater
    {
        public List<(Guid OrganizationId, DateTimeOffset UpdatedAtUtc)> Records { get; } = [];
        public bool ThrowOnIncrement { get; set; }

        public Task IncrementPolicyVersionAsync(Guid organizationId, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            if (ThrowOnIncrement)
            {
                throw new InvalidOperationException("Policy update failed.");
            }

            Records.Add((organizationId, updatedAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditRecord> Records { get; } = [];
        public bool ThrowOnWrite { get; set; }

        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Audit failed.");
            }

            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApplicationTransaction : IApplicationTransaction
    {
        public int BeginCalls { get; private set; }
        public int CommitCalls { get; private set; }

        public Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCalls++;
            return Task.FromResult<IApplicationTransactionScope>(new Scope(this));
        }

        private sealed class Scope(FakeApplicationTransaction owner) : IApplicationTransactionScope
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCalls++;
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FakeGuidProvider : IGuidProvider
    {
        private readonly Queue<Guid> _values = [];

        public void Enqueue(Guid value)
        {
            _values.Enqueue(value);
        }

        public Guid NewGuid()
        {
            return _values.Count == 0 ? Guid.NewGuid() : _values.Dequeue();
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
