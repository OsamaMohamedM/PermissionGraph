namespace PermissionGraph.Application.Tests;

public sealed class M02ApplicationUseCaseTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateOrganization_CreatesOwnerMembershipSeedAuditAndTransaction()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.GuidProvider.Enqueue(OrganizationId, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        var result = await fixture.CreateOrganizationHandler.HandleAsync(
            new CreateOrganizationCommand("Acme Engineering", "Platform team"),
            CancellationToken.None);

        result.Id.Should().Be(OrganizationId);
        result.OwnerUserId.Should().Be(OwnerId);
        fixture.Organizations.Items.Should().ContainSingle(item => item.Id == OrganizationId);
        fixture.Memberships.Items.Should().ContainSingle(item => item.OrganizationId == OrganizationId && item.UserId == OwnerId);
        fixture.SeedService.SeedCalls.Should().Be(1);
        fixture.AuditWriter.Records.Should().ContainSingle(item => item.Action == "organization.created");
        fixture.Transaction.BeginCalls.Should().Be(1);
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrganization_AllowsActiveMemberVisibility()
    {
        var fixture = UseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);

        var result = await fixture.GetOrganizationHandler.HandleAsync(
            new GetOrganizationQuery(OrganizationId),
            CancellationToken.None);

        result.Id.Should().Be(OrganizationId);
    }

    [Fact]
    public async Task GetOrganization_ReturnsNotFoundForCrossTenantUser()
    {
        var fixture = UseCaseFixture.Create(OtherUserId);
        fixture.AddOrganization();

        var act = () => fixture.GetOrganizationHandler.HandleAsync(new GetOrganizationQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task UpdateOrganization_RequiresOwner()
    {
        var fixture = UseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);

        var act = () => fixture.UpdateOrganizationHandler.HandleAsync(
            new UpdateOrganizationCommand(OrganizationId, "New Name", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_required");
    }

    [Fact]
    public async Task UseCase_RejectsMissingCurrentUser()
    {
        var fixture = UseCaseFixture.Create(null);

        var act = () => fixture.ListOrganizationsHandler.HandleAsync(new ListOrganizationsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task UseCase_RejectsInactiveCurrentUser()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.Users.Accounts[OwnerId] = fixture.Users.Accounts[OwnerId] with { IsActive = false };

        var act = () => fixture.ListOrganizationsHandler.HandleAsync(new ListOrganizationsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task AddOrganizationMember_RejectsDuplicateMembership()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);

        var act = () => fixture.AddOrganizationMemberHandler.HandleAsync(
            new AddOrganizationMemberCommand(OrganizationId, "member@permissiongraph.local"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "membership_already_exists");
    }

    [Fact]
    public async Task SuspendOrganizationMember_BlocksOwner()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddMembership(OwnerId);

        var act = () => fixture.SuspendOrganizationMemberHandler.HandleAsync(
            new SuspendOrganizationMemberCommand(OrganizationId, OwnerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_membership_cannot_be_suspended");
    }

    [Fact]
    public async Task RemoveOrganizationMember_BlocksOwner()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddMembership(OwnerId);

        var act = () => fixture.RemoveOrganizationMemberHandler.HandleAsync(
            new RemoveOrganizationMemberCommand(OrganizationId, OwnerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_membership_cannot_be_removed");
    }

    [Fact]
    public async Task ReactivateOrganizationMember_RejectsRemovedMembership()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var membership = fixture.AddMembership(MemberId);
        membership.Remove(isOwner: false, Now.AddMinutes(1));

        var act = () => fixture.ReactivateOrganizationMemberHandler.HandleAsync(
            new ReactivateOrganizationMemberCommand(OrganizationId, MemberId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "removed_membership_cannot_be_reactivated");
    }

    [Fact]
    public async Task TransferOwnership_RejectsNonMemberTarget()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();

        var act = () => fixture.TransferOwnershipHandler.HandleAsync(
            new TransferOwnershipCommand(OrganizationId, OtherUserId, "ValidPassword123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "target_owner_must_be_active_member");
    }

    [Fact]
    public async Task TransferOwnership_RejectsSuspendedOrRemovedTarget()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var membership = fixture.AddMembership(MemberId);
        membership.Suspend(isOwner: false, Now.AddMinutes(1));

        var act = () => fixture.TransferOwnershipHandler.HandleAsync(
            new TransferOwnershipCommand(OrganizationId, MemberId, "ValidPassword123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "target_owner_must_be_active_member");
    }

    [Fact]
    public async Task TransferOwnership_RejectsCurrentOwner()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddMembership(OwnerId);

        var act = () => fixture.TransferOwnershipHandler.HandleAsync(
            new TransferOwnershipCommand(OrganizationId, OwnerId, "ValidPassword123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "ownership_transfer_to_current_owner");
    }

    [Fact]
    public async Task TransferOwnership_RequiresRecentAuthentication()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        fixture.RecentAuthentication.HasRecentAuthentication = false;
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);

        var act = () => fixture.TransferOwnershipHandler.HandleAsync(
            new TransferOwnershipCommand(OrganizationId, MemberId, "ValidPassword123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "recent_authentication_required");
    }

    [Fact]
    public async Task TransferOwnership_ChangesOwnerAndCoordinatesTransactionAuditAndVersions()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        var organization = fixture.AddOrganization();
        fixture.AddMembership(OwnerId);
        fixture.AddMembership(MemberId);

        var result = await fixture.TransferOwnershipHandler.HandleAsync(
            new TransferOwnershipCommand(OrganizationId, MemberId, "ValidPassword123!"),
            CancellationToken.None);

        result.OwnerUserId.Should().Be(MemberId);
        organization.OwnerUserId.Should().Be(MemberId);
        fixture.Memberships.AuthorizationVersionIncrementCalls
            .Should()
            .BeEquivalentTo([(OrganizationId, OwnerId), (OrganizationId, MemberId)]);
        fixture.AuditWriter.Records.Should().ContainSingle(item => item.Action == "organization.ownership_transferred");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task ArchivedOrganization_BlocksNormalMutations()
    {
        var fixture = UseCaseFixture.Create(OwnerId);
        var organization = fixture.AddOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => fixture.AddOrganizationMemberHandler.HandleAsync(
            new AddOrganizationMemberCommand(OrganizationId, "member@permissiongraph.local"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task LeaveOrganization_BlocksOwnerAndAllowsActiveMember()
    {
        var ownerFixture = UseCaseFixture.Create(OwnerId);
        ownerFixture.AddOrganization();
        ownerFixture.AddMembership(OwnerId);

        var ownerAct = () => ownerFixture.LeaveOrganizationHandler.HandleAsync(
            new LeaveOrganizationCommand(OrganizationId),
            CancellationToken.None);

        await ownerAct.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_membership_cannot_be_removed");

        var memberFixture = UseCaseFixture.Create(MemberId);
        memberFixture.AddOrganization();
        var memberMembership = memberFixture.AddMembership(MemberId);

        await memberFixture.LeaveOrganizationHandler.HandleAsync(
            new LeaveOrganizationCommand(OrganizationId),
            CancellationToken.None);

        memberMembership.Status.Should().Be(MembershipStatus.Removed);
        memberFixture.AuditWriter.Records.Should().ContainSingle(item => item.Action == "organization_member.left");
    }

    private sealed class UseCaseFixture
    {
        private UseCaseFixture(Guid? currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Organizations = new FakeOrganizationRepository();
            Memberships = new FakeOrganizationMembershipRepository();
            AuditWriter = new FakeAuditWriter();
            Transaction = new FakeApplicationTransaction();
            SeedService = new FakeOrganizationSeedService();
            RecentAuthentication = new FakeRecentAuthenticationVerifier();
            GuidProvider = new FakeGuidProvider();
            Clock = new FakeClock(Now);

            if (currentUserId is not null)
            {
                Users.Accounts[currentUserId.Value] = new UserAccount(
                    currentUserId.Value,
                    "current@permissiongraph.local",
                    "Current User",
                    IsActive: true);
            }

            Users.Accounts[OwnerId] = new UserAccount(OwnerId, "owner@permissiongraph.local", "Owner", IsActive: true);
            Users.Accounts[MemberId] = new UserAccount(MemberId, "member@permissiongraph.local", "Member", IsActive: true);
            Users.Accounts[OtherUserId] = new UserAccount(OtherUserId, "other@permissiongraph.local", "Other", IsActive: true);

            var resolver = new AuthenticatedUserResolver(CurrentUser, Users);
            var access = new OrganizationAccessHelper(Organizations, Memberships);

            CreateOrganizationHandler = new CreateOrganizationHandler(
                new CreateOrganizationCommandValidator(),
                resolver,
                Organizations,
                Memberships,
                SeedService,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            ListOrganizationsHandler = new ListOrganizationsHandler(new ListOrganizationsQueryValidator(), resolver, Organizations);
            GetOrganizationHandler = new GetOrganizationHandler(new GetOrganizationQueryValidator(), resolver, access);
            UpdateOrganizationHandler = new UpdateOrganizationHandler(new UpdateOrganizationCommandValidator(), resolver, access, AuditWriter, Transaction, Clock);
            ArchiveOrganizationHandler = new ArchiveOrganizationHandler(new ArchiveOrganizationCommandValidator(), resolver, access, AuditWriter, Transaction, Clock);
            TransferOwnershipHandler = new TransferOwnershipHandler(
                new TransferOwnershipCommandValidator(),
                resolver,
                access,
                Memberships,
                Users,
                RecentAuthentication,
                AuditWriter,
                Transaction,
                Clock);
            AddOrganizationMemberHandler = new AddOrganizationMemberHandler(
                new AddOrganizationMemberCommandValidator(),
                resolver,
                access,
                Memberships,
                Users,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            GetOrganizationMemberHandler = new GetOrganizationMemberHandler(new GetOrganizationMemberQueryValidator(), resolver, access, Memberships);
            ListOrganizationMembersHandler = new ListOrganizationMembersHandler(new ListOrganizationMembersQueryValidator(), resolver, access, Memberships);
            SuspendOrganizationMemberHandler = new SuspendOrganizationMemberHandler(new SuspendOrganizationMemberCommandValidator(), resolver, access, Memberships, AuditWriter, Transaction, Clock);
            ReactivateOrganizationMemberHandler = new ReactivateOrganizationMemberHandler(new ReactivateOrganizationMemberCommandValidator(), resolver, access, Memberships, AuditWriter, Transaction, Clock);
            RemoveOrganizationMemberHandler = new RemoveOrganizationMemberHandler(new RemoveOrganizationMemberCommandValidator(), resolver, access, Memberships, AuditWriter, Transaction, Clock);
            LeaveOrganizationHandler = new LeaveOrganizationHandler(new LeaveOrganizationCommandValidator(), resolver, access, Memberships, AuditWriter, Transaction, Clock);
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeOrganizationRepository Organizations { get; }
        public FakeOrganizationMembershipRepository Memberships { get; }
        public FakeAuditWriter AuditWriter { get; }
        public FakeApplicationTransaction Transaction { get; }
        public FakeOrganizationSeedService SeedService { get; }
        public FakeRecentAuthenticationVerifier RecentAuthentication { get; }
        public FakeGuidProvider GuidProvider { get; }
        public FakeClock Clock { get; }
        public CreateOrganizationHandler CreateOrganizationHandler { get; }
        public ListOrganizationsHandler ListOrganizationsHandler { get; }
        public GetOrganizationHandler GetOrganizationHandler { get; }
        public UpdateOrganizationHandler UpdateOrganizationHandler { get; }
        public ArchiveOrganizationHandler ArchiveOrganizationHandler { get; }
        public TransferOwnershipHandler TransferOwnershipHandler { get; }
        public AddOrganizationMemberHandler AddOrganizationMemberHandler { get; }
        public GetOrganizationMemberHandler GetOrganizationMemberHandler { get; }
        public ListOrganizationMembersHandler ListOrganizationMembersHandler { get; }
        public SuspendOrganizationMemberHandler SuspendOrganizationMemberHandler { get; }
        public ReactivateOrganizationMemberHandler ReactivateOrganizationMemberHandler { get; }
        public RemoveOrganizationMemberHandler RemoveOrganizationMemberHandler { get; }
        public LeaveOrganizationHandler LeaveOrganizationHandler { get; }

        public static UseCaseFixture Create(Guid? currentUserId)
        {
            return new UseCaseFixture(currentUserId);
        }

        public Organization AddOrganization()
        {
            var organization = Organization.Create(
                OrganizationId,
                "Acme Engineering",
                "ACME ENGINEERING",
                null,
                OwnerId,
                Now);

            Organizations.Items.Add(organization);
            return organization;
        }

        public OrganizationMembership AddMembership(Guid userId)
        {
            var membership = OrganizationMembership.CreateActive(Guid.NewGuid(), OrganizationId, userId, Now, Now);
            Memberships.Items.Add(membership);
            return membership;
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
            var account = Accounts.Values.SingleOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(account);
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
            return Task.FromResult(new PagedResult<Organization>(Items.Take(pageSize).ToArray(), null));
        }
    }

    private sealed class FakeOrganizationMembershipRepository : IOrganizationMembershipRepository
    {
        public List<OrganizationMembership> Items { get; } = [];

        public List<(Guid OrganizationId, Guid UserId)> AuthorizationVersionIncrementCalls { get; } = [];

        public Task AddAsync(OrganizationMembership membership, CancellationToken cancellationToken)
        {
            Items.Add(membership);
            return Task.CompletedTask;
        }

        public Task<OrganizationMembership?> GetByOrganizationAndUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.OrganizationId == organizationId &&
                item.UserId == userId &&
                item.Status != MembershipStatus.Removed));
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
            var members = Items
                .Where(item => item.OrganizationId == organizationId && item.Status != MembershipStatus.Removed)
                .Take(pageSize)
                .Select(item => OrganizationMemberResult.FromDomain(item))
                .ToArray();

            return Task.FromResult(new PagedResult<OrganizationMemberResult>(members, null));
        }

        public Task<OrganizationMemberResult?> GetMemberResultAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            var membership = Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.UserId == userId);
            return Task.FromResult(membership is null ? null : OrganizationMemberResult.FromDomain(membership));
        }

        public Task IncrementAuthorizationVersionAsync(Guid organizationId, Guid userId, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            AuthorizationVersionIncrementCalls.Add((organizationId, userId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditRecord> Records { get; } = [];

        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
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
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCalls++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeOrganizationSeedService : IOrganizationSeedService
    {
        public int SeedCalls { get; private set; }

        public Task SeedDefaultAuthorizationAsync(Organization organization, Guid actorUserId, CancellationToken cancellationToken)
        {
            SeedCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecentAuthenticationVerifier : IRecentAuthenticationVerifier
    {
        public bool HasRecentAuthentication { get; set; } = true;

        public Task<bool> HasRecentAuthenticationAsync(Guid userId, string currentPassword, CancellationToken cancellationToken)
        {
            return Task.FromResult(HasRecentAuthentication);
        }
    }

    private sealed class FakeGuidProvider : IGuidProvider
    {
        private readonly Queue<Guid> _ids = [];

        public void Enqueue(params Guid[] ids)
        {
            foreach (var id in ids)
            {
                _ids.Enqueue(id);
            }
        }

        public Guid NewGuid()
        {
            return _ids.Count == 0 ? Guid.NewGuid() : _ids.Dequeue();
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}