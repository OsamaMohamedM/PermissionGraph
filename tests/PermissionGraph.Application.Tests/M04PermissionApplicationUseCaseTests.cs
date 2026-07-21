namespace PermissionGraph.Application.Tests;

public sealed class M04PermissionApplicationUseCaseTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlatformPermissionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CustomPermissionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PermissionUseCase_RejectsMissingCurrentUser()
    {
        var fixture = PermissionUseCaseFixture.Create(null);

        var act = () => fixture.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task PermissionUseCase_RejectsInactiveAccount()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        fixture.Users.Accounts[OwnerId] = fixture.Users.Accounts[OwnerId] with { IsActive = false };

        var act = () => fixture.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task ListAndGet_ReturnSafeNotFoundForNonMemberSuspendedOrRemovedMember()
    {
        var nonMember = PermissionUseCaseFixture.Create(OtherUserId);
        nonMember.AddOrganization();
        nonMember.AddPlatformPermission();

        var nonMemberList = () => nonMember.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId), CancellationToken.None);

        await nonMemberList.Should().ThrowAsync<NotFoundApplicationException>();

        var suspended = PermissionUseCaseFixture.Create(MemberId);
        suspended.AddOrganization();
        var suspendedMembership = suspended.AddMembership(MemberId);
        suspendedMembership.Suspend(isOwner: false, Now.AddMinutes(1));

        var suspendedGet = () => suspended.GetPermissionHandler.HandleAsync(new GetPermissionQuery(OrganizationId, PlatformPermissionId), CancellationToken.None);

        await suspendedGet.Should().ThrowAsync<NotFoundApplicationException>();

        var removed = PermissionUseCaseFixture.Create(MemberId);
        removed.AddOrganization();
        var removedMembership = removed.AddMembership(MemberId);
        removedMembership.Remove(isOwner: false, Now.AddMinutes(1));

        var removedList = () => removed.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId), CancellationToken.None);

        await removedList.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task ActiveMember_CanListAndGetPlatformAndOrganizationCustomPermissionsOnly()
    {
        var fixture = PermissionUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        var platform = fixture.AddPlatformPermission();
        var custom = fixture.AddCustomPermission();
        fixture.AddCustomPermission(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), OtherOrganizationId, "reports.export");

        var list = await fixture.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId), CancellationToken.None);
        var getPlatform = await fixture.GetPermissionHandler.HandleAsync(new GetPermissionQuery(OrganizationId, platform.Id), CancellationToken.None);
        var getCustom = await fixture.GetPermissionHandler.HandleAsync(new GetPermissionQuery(OrganizationId, custom.Id), CancellationToken.None);
        var getCrossTenant = () => fixture.GetPermissionHandler.HandleAsync(
            new GetPermissionQuery(OrganizationId, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")),
            CancellationToken.None);

        list.Items.Should().Contain(permission => permission.Id == platform.Id);
        list.Items.Should().Contain(permission => permission.Id == custom.Id);
        list.Items.Should().NotContain(permission => permission.OrganizationId == OtherOrganizationId);
        getPlatform.PermissionType.Should().Be(PermissionType.Platform);
        getCustom.OrganizationId.Should().Be(OrganizationId);
        await getCrossTenant.Should().ThrowAsync<NotFoundApplicationException>();
        fixture.Permissions.ListCalls.Should().ContainSingle(call => call.OrganizationId == OrganizationId);
        fixture.Permissions.VisibleGetCalls.Should().Contain(call => call.OrganizationId == OrganizationId && call.PermissionId == platform.Id);
    }

    [Fact]
    public async Task NonOwner_CannotMutateCustomPermissions()
    {
        var fixture = PermissionUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        fixture.AddCustomPermission();

        var create = () => fixture.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "documents.view", "View documents", null, "Documents", PermissionAllowedScopes.Organization, true),
            CancellationToken.None);
        var update = () => fixture.UpdateCustomPermissionHandler.HandleAsync(
            new UpdateCustomPermissionCommand(OrganizationId, CustomPermissionId, "Review documents", null, "Documents", true),
            CancellationToken.None);
        var archive = () => fixture.ArchiveCustomPermissionHandler.HandleAsync(new ArchiveCustomPermissionCommand(OrganizationId, CustomPermissionId), CancellationToken.None);
        var activate = () => fixture.ActivateCustomPermissionHandler.HandleAsync(new ActivateCustomPermissionCommand(OrganizationId, CustomPermissionId), CancellationToken.None);

        await create.Should().ThrowAsync<ForbiddenApplicationException>();
        await update.Should().ThrowAsync<ForbiddenApplicationException>();
        await archive.Should().ThrowAsync<ForbiddenApplicationException>();
        await activate.Should().ThrowAsync<ForbiddenApplicationException>();
    }

    [Fact]
    public async Task OwnerCreate_AddsCustomPermissionPolicyVersionAuditAndTransaction()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        fixture.GuidProvider.Enqueue(CustomPermissionId);
        fixture.AddOrganization();

        var result = await fixture.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "documents.approve", "Approve documents", "Approve docs", "Documents", PermissionAllowedScopes.Project, true),
            CancellationToken.None);

        result.Id.Should().Be(CustomPermissionId);
        result.NormalizedKey.Should().Be("documents.approve");
        fixture.Permissions.Items.Should().ContainSingle(permission => permission.Id == CustomPermissionId);
        fixture.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "permission.created");
        fixture.Transaction.BeginCalls.Should().Be(1);
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task Create_RejectsReservedPrefixAndDuplicateNormalizedKey()
    {
        var reserved = PermissionUseCaseFixture.Create(OwnerId);
        reserved.AddOrganization();

        var reservedAct = () => reserved.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "pg.custom.view", "View custom", null, "Custom", PermissionAllowedScopes.Organization, true),
            CancellationToken.None);

        await reservedAct.Should().ThrowAsync<CommandValidationException>();

        var duplicate = PermissionUseCaseFixture.Create(OwnerId);
        duplicate.AddOrganization();
        duplicate.AddCustomPermission(key: "documents.approve");

        var duplicateAct = () => duplicate.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "documents.approve", "Approve documents", null, "Documents", PermissionAllowedScopes.Organization, true),
            CancellationToken.None);

        await duplicateAct.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "permission_key_already_exists");
    }

    [Fact]
    public async Task UpdateMetadata_ChangesMutableFieldsAndOnlyRequestableChangeIncrementsPolicyVersion()
    {
        var metadataOnly = PermissionUseCaseFixture.Create(OwnerId);
        metadataOnly.AddOrganization();
        metadataOnly.AddCustomPermission(isRequestable: true);

        var metadataResult = await metadataOnly.UpdateCustomPermissionHandler.HandleAsync(
            new UpdateCustomPermissionCommand(OrganizationId, CustomPermissionId, "Review documents", "Review docs", "Reviews", true),
            CancellationToken.None);

        metadataResult.DisplayName.Should().Be("Review documents");
        metadataResult.Key.Should().Be("documents.approve");
        metadataResult.AllowedScopes.Should().Be(PermissionAllowedScopes.Project);
        metadataOnly.PolicyVersion.Records.Should().BeEmpty();
        metadataOnly.AuditWriter.Records.Should().ContainSingle(record => record.Action == "permission.updated");
        metadataOnly.Transaction.CommitCalls.Should().Be(1);

        var requestability = PermissionUseCaseFixture.Create(OwnerId);
        requestability.AddOrganization();
        requestability.AddCustomPermission(isRequestable: true);

        await requestability.UpdateCustomPermissionHandler.HandleAsync(
            new UpdateCustomPermissionCommand(OrganizationId, CustomPermissionId, "Review documents", "Review docs", "Reviews", false),
            CancellationToken.None);

        requestability.PolicyVersion.Records.Should().ContainSingle(record => record.OrganizationId == OrganizationId);
    }

    [Fact]
    public async Task ArchiveAndActivate_IncrementPolicyVersionAuditAndRejectRepeatedLifecycle()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var permission = fixture.AddCustomPermission();

        await fixture.ArchiveCustomPermissionHandler.HandleAsync(
            new ArchiveCustomPermissionCommand(OrganizationId, permission.Id),
            CancellationToken.None);

        permission.IsActive.Should().BeFalse();
        fixture.PolicyVersion.Records.Should().Contain(record => record.OrganizationId == OrganizationId);
        fixture.AuditWriter.Records.Should().Contain(record => record.Action == "permission.archived");

        var repeatedArchive = () => fixture.ArchiveCustomPermissionHandler.HandleAsync(
            new ArchiveCustomPermissionCommand(OrganizationId, permission.Id),
            CancellationToken.None);

        await repeatedArchive.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "permission_already_archived");

        await fixture.ActivateCustomPermissionHandler.HandleAsync(
            new ActivateCustomPermissionCommand(OrganizationId, permission.Id),
            CancellationToken.None);

        permission.IsActive.Should().BeTrue();
        fixture.PolicyVersion.Records.Should().HaveCount(2);
        fixture.AuditWriter.Records.Should().Contain(record => record.Action == "permission.activated");

        var repeatedActivate = () => fixture.ActivateCustomPermissionHandler.HandleAsync(
            new ActivateCustomPermissionCommand(OrganizationId, permission.Id),
            CancellationToken.None);

        await repeatedActivate.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "permission_already_active");
    }

    [Fact]
    public async Task PlatformOrCrossTenantMutation_ReturnsSafeNotFound()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var platform = fixture.AddPlatformPermission();
        var other = fixture.AddCustomPermission(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), OtherOrganizationId, "reports.export");

        var platformUpdate = () => fixture.UpdateCustomPermissionHandler.HandleAsync(
            new UpdateCustomPermissionCommand(OrganizationId, platform.Id, "Rename", null, "Reports", true),
            CancellationToken.None);
        var crossTenantArchive = () => fixture.ArchiveCustomPermissionHandler.HandleAsync(
            new ArchiveCustomPermissionCommand(OrganizationId, other.Id),
            CancellationToken.None);

        await platformUpdate.Should().ThrowAsync<NotFoundApplicationException>();
        await crossTenantArchive.Should().ThrowAsync<NotFoundApplicationException>();
        fixture.Permissions.CustomGetCalls.Should().Contain(call => call.OrganizationId == OrganizationId && call.PermissionId == platform.Id);
    }

    [Fact]
    public async Task ArchivedOrganizationMutation_ReturnsSafeNotFound()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        var organization = fixture.AddOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => fixture.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "documents.view", "View documents", null, "Documents", PermissionAllowedScopes.Organization, true),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task AuditFailure_DoesNotCommitTransaction()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AuditWriter.ThrowOnWrite = true;

        var act = () => fixture.CreateCustomPermissionHandler.HandleAsync(
            new CreateCustomPermissionCommand(OrganizationId, "documents.view", "View documents", null, "Documents", PermissionAllowedScopes.Organization, true),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fixture.Transaction.BeginCalls.Should().Be(1);
        fixture.Transaction.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task List_ValidatesPaginationAndFilters()
    {
        var fixture = PermissionUseCaseFixture.Create(OwnerId);

        var invalidPage = () => fixture.ListPermissionsHandler.HandleAsync(new ListPermissionsQuery(OrganizationId, Page: 0), CancellationToken.None);
        var invalidType = () => fixture.ListPermissionsHandler.HandleAsync(
            new ListPermissionsQuery(OrganizationId, PermissionType: (PermissionType)99),
            CancellationToken.None);

        await invalidPage.Should().ThrowAsync<CommandValidationException>();
        await invalidType.Should().ThrowAsync<CommandValidationException>();
    }

    private sealed class PermissionUseCaseFixture
    {
        private PermissionUseCaseFixture(Guid? currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Organizations = new FakeOrganizationRepository();
            Memberships = new FakeOrganizationMembershipRepository();
            Permissions = new FakePermissionDefinitionRepository();
            PolicyVersion = new FakeOrganizationPolicyVersionUpdater();
            AuditWriter = new FakeAuditWriter();
            Transaction = new FakeApplicationTransaction();
            GuidProvider = new FakeGuidProvider();
            Clock = new FakeClock(Now);

            if (currentUserId is not null)
            {
                Users.Accounts[currentUserId.Value] = new UserAccount(
                    currentUserId.Value,
                    $"{currentUserId}@example.test",
                    "Current User",
                    IsActive: true);
            }

            var resolver = new AuthenticatedUserResolver(CurrentUser, Users);
            var organizationAccess = new OrganizationAccessHelper(Organizations, Memberships);
            var permissionCatalogAccess = new PermissionCatalogAccessHelper(organizationAccess, Permissions);

            ListPermissionsHandler = new ListPermissionsHandler(
                new ListPermissionsQueryValidator(),
                resolver,
                permissionCatalogAccess,
                Permissions);
            GetPermissionHandler = new GetPermissionHandler(new GetPermissionQueryValidator(), resolver, permissionCatalogAccess);
            CreateCustomPermissionHandler = new CreateCustomPermissionHandler(
                new CreateCustomPermissionCommandValidator(),
                resolver,
                permissionCatalogAccess,
                Permissions,
                PolicyVersion,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            UpdateCustomPermissionHandler = new UpdateCustomPermissionHandler(
                new UpdateCustomPermissionCommandValidator(),
                resolver,
                permissionCatalogAccess,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
            ArchiveCustomPermissionHandler = new ArchiveCustomPermissionHandler(
                new ArchiveCustomPermissionCommandValidator(),
                resolver,
                permissionCatalogAccess,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
            ActivateCustomPermissionHandler = new ActivateCustomPermissionHandler(
                new ActivateCustomPermissionCommandValidator(),
                resolver,
                permissionCatalogAccess,
                PolicyVersion,
                AuditWriter,
                Transaction,
                Clock);
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeOrganizationRepository Organizations { get; }
        public FakeOrganizationMembershipRepository Memberships { get; }
        public FakePermissionDefinitionRepository Permissions { get; }
        public FakeOrganizationPolicyVersionUpdater PolicyVersion { get; }
        public FakeAuditWriter AuditWriter { get; }
        public FakeApplicationTransaction Transaction { get; }
        public FakeGuidProvider GuidProvider { get; }
        public FakeClock Clock { get; }
        public ListPermissionsHandler ListPermissionsHandler { get; }
        public GetPermissionHandler GetPermissionHandler { get; }
        public CreateCustomPermissionHandler CreateCustomPermissionHandler { get; }
        public UpdateCustomPermissionHandler UpdateCustomPermissionHandler { get; }
        public ArchiveCustomPermissionHandler ArchiveCustomPermissionHandler { get; }
        public ActivateCustomPermissionHandler ActivateCustomPermissionHandler { get; }

        public static PermissionUseCaseFixture Create(Guid? currentUserId)
        {
            return new PermissionUseCaseFixture(currentUserId);
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
            var membership = OrganizationMembership.CreateActive(
                Guid.NewGuid(),
                organizationId ?? OrganizationId,
                userId,
                Now,
                Now);
            Memberships.Items.Add(membership);
            return membership;
        }

        public PermissionDefinition AddPlatformPermission()
        {
            var permission = PermissionDefinition.CreatePlatform(
                PlatformPermissionId,
                "pg.permissions.view",
                "pg.permissions.view",
                "View permissions",
                null,
                "Permissions",
                PermissionAllowedScopes.Organization,
                false,
                Now);
            Permissions.Items.Add(permission);
            return permission;
        }

        public PermissionDefinition AddCustomPermission(
            Guid? permissionId = null,
            Guid? organizationId = null,
            string key = "documents.approve",
            bool isRequestable = true)
        {
            var permission = PermissionDefinition.CreateCustom(
                permissionId ?? CustomPermissionId,
                organizationId ?? OrganizationId,
                key,
                key,
                "Approve documents",
                null,
                "Documents",
                PermissionAllowedScopes.Project,
                isRequestable,
                Now);
            Permissions.Items.Add(permission);
            return permission;
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

        public Task<PagedResult<OrganizationMemberResult>> ListMembersAsync(
            Guid organizationId,
            int pageSize,
            string? cursor,
            string? search,
            string? status,
            CancellationToken cancellationToken)
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

    private sealed class FakePermissionDefinitionRepository : IPermissionDefinitionRepository
    {
        public List<PermissionDefinition> Items { get; } = [];
        public List<(Guid OrganizationId, PermissionDefinitionListFilters Filters, int Page, int PageSize)> ListCalls { get; } = [];
        public List<(Guid OrganizationId, Guid PermissionId)> VisibleGetCalls { get; } = [];
        public List<(Guid OrganizationId, Guid PermissionId)> CustomGetCalls { get; } = [];
        public List<(Guid OrganizationId, string NormalizedKey, Guid? ExcludingPermissionId)> DuplicateChecks { get; } = [];

        public Task AddAsync(PermissionDefinition permission, CancellationToken cancellationToken)
        {
            Items.Add(permission);
            return Task.CompletedTask;
        }

        public Task<PermissionDefinition?> GetVisibleByOrganizationAndIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            VisibleGetCalls.Add((organizationId, permissionId));
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.Id == permissionId &&
                (item.PermissionType == PermissionType.Platform || item.OrganizationId == organizationId)));
        }

        public Task<PermissionDefinition?> GetOrganizationCustomByIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            CustomGetCalls.Add((organizationId, permissionId));
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.Id == permissionId &&
                item.PermissionType == PermissionType.Custom &&
                item.OrganizationId == organizationId));
        }

        public Task<PageResult<PermissionDefinition>> ListVisibleForOrganizationAsync(
            Guid organizationId,
            PermissionDefinitionListFilters filters,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            ListCalls.Add((organizationId, filters, page, pageSize));
            var query = Items.Where(item => item.PermissionType == PermissionType.Platform || item.OrganizationId == organizationId);

            if (filters.PermissionType is not null)
            {
                query = query.Where(item => item.PermissionType == filters.PermissionType);
            }

            if (filters.Module is not null)
            {
                query = query.Where(item => item.Module == filters.Module);
            }

            if (filters.IsActive is not null)
            {
                query = query.Where(item => item.IsActive == filters.IsActive);
            }

            if (filters.IsRequestable is not null)
            {
                query = query.Where(item => item.IsRequestable == filters.IsRequestable);
            }

            if (filters.AllowedScopes is not null)
            {
                query = query.Where(item => item.AllowedScopes == filters.AllowedScopes);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                query = query.Where(item => item.Key.Contains(filters.Search, StringComparison.OrdinalIgnoreCase));
            }

            var all = query.ToArray();
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(new PageResult<PermissionDefinition>(items, page, pageSize, all.Length));
        }

        public Task<bool> CustomNormalizedKeyExistsAsync(
            Guid organizationId,
            string normalizedKey,
            Guid? excludingPermissionId,
            CancellationToken cancellationToken)
        {
            DuplicateChecks.Add((organizationId, normalizedKey, excludingPermissionId));
            var exists = Items.Any(item =>
                item.OrganizationId == organizationId &&
                item.PermissionType == PermissionType.Custom &&
                item.Id != excludingPermissionId &&
                string.Equals(item.NormalizedKey, normalizedKey, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }

    private sealed class FakeOrganizationPolicyVersionUpdater : IOrganizationPolicyVersionUpdater
    {
        public List<(Guid OrganizationId, DateTimeOffset UpdatedAtUtc)> Records { get; } = [];

        public Task IncrementPolicyVersionAsync(Guid organizationId, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
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