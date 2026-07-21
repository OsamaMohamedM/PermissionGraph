namespace PermissionGraph.Application.Tests;

public sealed class M03ProjectApplicationUseCaseTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ProjectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectUseCase_RejectsMissingCurrentUser()
    {
        var fixture = ProjectUseCaseFixture.Create(null);

        var act = () => fixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task ProjectUseCase_RejectsInactiveCurrentUser()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.Users.Accounts[OwnerId] = fixture.Users.Accounts[OwnerId] with { IsActive = false };

        var act = () => fixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedApplicationException>();
    }

    [Fact]
    public async Task ListProjects_ReturnsNotFoundForNonMember()
    {
        var fixture = ProjectUseCaseFixture.Create(OtherUserId);
        fixture.AddOrganization();

        var act = () => fixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task ListProjects_ReturnsNotFoundForSuspendedOrRemovedMembership()
    {
        var suspendedFixture = ProjectUseCaseFixture.Create(MemberId);
        suspendedFixture.AddOrganization();
        var suspended = suspendedFixture.AddMembership(MemberId);
        suspended.Suspend(isOwner: false, Now.AddMinutes(1));

        var suspendedAct = () => suspendedFixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);

        await suspendedAct.Should().ThrowAsync<NotFoundApplicationException>();

        var removedFixture = ProjectUseCaseFixture.Create(MemberId);
        removedFixture.AddOrganization();
        var removed = removedFixture.AddMembership(MemberId);
        removed.Remove(isOwner: false, Now.AddMinutes(1));

        var removedAct = () => removedFixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);

        await removedAct.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task ActiveMember_CanListAndGetProjects()
    {
        var fixture = ProjectUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        fixture.AddProject();

        var list = await fixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId), CancellationToken.None);
        var get = await fixture.GetProjectHandler.HandleAsync(new GetProjectQuery(OrganizationId, ProjectId), CancellationToken.None);

        list.Items.Should().ContainSingle(project => project.Id == ProjectId);
        get.Id.Should().Be(ProjectId);
        fixture.Projects.ListCalls.Should().ContainSingle(call => call.OrganizationId == OrganizationId);
        fixture.Projects.GetCalls.Should().ContainSingle(call => call.OrganizationId == OrganizationId && call.ProjectId == ProjectId);
    }

    [Fact]
    public async Task NonOwner_CannotCreateUpdateOrArchiveProject()
    {
        var fixture = ProjectUseCaseFixture.Create(MemberId);
        fixture.AddOrganization();
        fixture.AddMembership(MemberId);
        fixture.AddProject();

        var create = () => fixture.CreateProjectHandler.HandleAsync(new CreateProjectCommand(OrganizationId, "Billing Portal", null), CancellationToken.None);
        var update = () => fixture.UpdateProjectHandler.HandleAsync(new UpdateProjectCommand(OrganizationId, ProjectId, "Billing Portal", null), CancellationToken.None);
        var archive = () => fixture.ArchiveProjectHandler.HandleAsync(new ArchiveProjectCommand(OrganizationId, ProjectId, "ARCHIVE"), CancellationToken.None);

        await create.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_required");
        await update.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_required");
        await archive.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "owner_required");
    }

    [Fact]
    public async Task CreateProject_CreatesProjectAdministratorAssignmentAuditAndTransaction()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.GuidProvider.Enqueue(ProjectId);
        fixture.AddOrganization();

        var result = await fixture.CreateProjectHandler.HandleAsync(
            new CreateProjectCommand(OrganizationId, "Launch Control", "Coordinate launch."),
            CancellationToken.None);

        result.Id.Should().Be(ProjectId);
        result.OrganizationId.Should().Be(OrganizationId);
        result.NormalizedName.Should().Be("LAUNCH CONTROL");
        fixture.Projects.Items.Should().ContainSingle(project => project.Id == ProjectId && project.OrganizationId == OrganizationId);
        fixture.ProjectAdministratorAssignments.Calls.Should().ContainSingle(call => call.ProjectId == ProjectId && call.CreatorUserId == OwnerId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "project.created");
        fixture.Transaction.BeginCalls.Should().Be(1);
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task CreateProject_ReturnsConflictForDuplicateActiveNormalizedName()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddProject(name: "Launch Control", normalizedName: "LAUNCH CONTROL");

        var act = () => fixture.CreateProjectHandler.HandleAsync(
            new CreateProjectCommand(OrganizationId, " launch control ", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "project_name_already_exists");
    }

    [Fact]
    public async Task CreateProject_ReturnsNotFoundForArchivedOrganization()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        var organization = fixture.AddOrganization();
        organization.Archive(Now.AddMinutes(1));

        var act = () => fixture.CreateProjectHandler.HandleAsync(
            new CreateProjectCommand(OrganizationId, "Launch Control", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>();
    }

    [Fact]
    public async Task UpdateProject_UpdatesOwnedProjectAndUsesScopedLookup()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddProject();

        var result = await fixture.UpdateProjectHandler.HandleAsync(
            new UpdateProjectCommand(OrganizationId, ProjectId, "Billing Portal", "Updated"),
            CancellationToken.None);

        result.Name.Should().Be("Billing Portal");
        result.NormalizedName.Should().Be("BILLING PORTAL");
        fixture.Projects.GetCalls.Should().ContainSingle(call => call.OrganizationId == OrganizationId && call.ProjectId == ProjectId);
        fixture.Projects.ActiveNameChecks.Should().ContainSingle(call =>
            call.OrganizationId == OrganizationId &&
            call.NormalizedName == "BILLING PORTAL" &&
            call.ExcludingProjectId == ProjectId);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "project.updated");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProject_ReturnsSafeNotFoundForRouteOrganizationMismatch()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.Organizations.Items.Add(Organization.Create(OtherOrganizationId, "Other Org", "OTHER ORG", null, OwnerId, Now));
        fixture.AddProject(organizationId: OtherOrganizationId);

        var act = () => fixture.UpdateProjectHandler.HandleAsync(
            new UpdateProjectCommand(OrganizationId, ProjectId, "Billing Portal", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>()
            .Where(exception => exception.ErrorCode == "project_not_found");
    }

    [Fact]
    public async Task UpdateProject_ReturnsConflictForDuplicateActiveNormalizedName()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        fixture.AddProject();
        fixture.AddProject(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), name: "Billing Portal", normalizedName: "BILLING PORTAL");

        var act = () => fixture.UpdateProjectHandler.HandleAsync(
            new UpdateProjectCommand(OrganizationId, ProjectId, "billing portal", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "project_name_already_exists");
    }

    [Fact]
    public async Task UpdateAndArchive_ReturnConflictForArchivedProjectMutation()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var project = fixture.AddProject();
        project.Archive(Now.AddMinutes(1));

        var update = () => fixture.UpdateProjectHandler.HandleAsync(
            new UpdateProjectCommand(OrganizationId, ProjectId, "Billing Portal", null),
            CancellationToken.None);
        var archive = () => fixture.ArchiveProjectHandler.HandleAsync(
            new ArchiveProjectCommand(OrganizationId, ProjectId, "ARCHIVE"),
            CancellationToken.None);

        await update.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "archived_project_cannot_be_updated");
        await archive.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "project_already_archived");
    }

    [Fact]
    public async Task ArchiveProject_ArchivesOwnedProjectAndWritesAudit()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);
        fixture.AddOrganization();
        var project = fixture.AddProject();

        await fixture.ArchiveProjectHandler.HandleAsync(
            new ArchiveProjectCommand(OrganizationId, ProjectId, "ARCHIVE"),
            CancellationToken.None);

        project.Status.Should().Be(ProjectStatus.Archived);
        project.ArchivedAtUtc.Should().Be(Now);
        fixture.AuditWriter.Records.Should().ContainSingle(record => record.Action == "project.archived");
        fixture.Transaction.BeginCalls.Should().Be(1);
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task ListProjects_RejectsInvalidPagination()
    {
        var fixture = ProjectUseCaseFixture.Create(OwnerId);

        var act = () => fixture.ListProjectsHandler.HandleAsync(new ListProjectsQuery(OrganizationId, Page: 0), CancellationToken.None);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    private sealed class ProjectUseCaseFixture
    {
        private ProjectUseCaseFixture(Guid? currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Organizations = new FakeOrganizationRepository();
            Memberships = new FakeOrganizationMembershipRepository();
            Projects = new FakeProjectRepository();
            ProjectAdministratorAssignments = new FakeProjectAdministratorAssignmentService();
            AuditWriter = new FakeAuditWriter();
            Transaction = new FakeApplicationTransaction();
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
            var organizationAccess = new OrganizationAccessHelper(Organizations, Memberships);
            var projectAccess = new ProjectAccessHelper(organizationAccess, Projects);

            CreateProjectHandler = new CreateProjectHandler(
                new CreateProjectCommandValidator(),
                resolver,
                projectAccess,
                Projects,
                ProjectAdministratorAssignments,
                AuditWriter,
                Transaction,
                GuidProvider,
                Clock);
            ListProjectsHandler = new ListProjectsHandler(new ListProjectsQueryValidator(), resolver, projectAccess, Projects);
            GetProjectHandler = new GetProjectHandler(new GetProjectQueryValidator(), resolver, projectAccess);
            UpdateProjectHandler = new UpdateProjectHandler(new UpdateProjectCommandValidator(), resolver, projectAccess, Projects, AuditWriter, Transaction, Clock);
            ArchiveProjectHandler = new ArchiveProjectHandler(new ArchiveProjectCommandValidator(), resolver, projectAccess, AuditWriter, Transaction, Clock);
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeOrganizationRepository Organizations { get; }
        public FakeOrganizationMembershipRepository Memberships { get; }
        public FakeProjectRepository Projects { get; }
        public FakeProjectAdministratorAssignmentService ProjectAdministratorAssignments { get; }
        public FakeAuditWriter AuditWriter { get; }
        public FakeApplicationTransaction Transaction { get; }
        public FakeGuidProvider GuidProvider { get; }
        public FakeClock Clock { get; }
        public CreateProjectHandler CreateProjectHandler { get; }
        public ListProjectsHandler ListProjectsHandler { get; }
        public GetProjectHandler GetProjectHandler { get; }
        public UpdateProjectHandler UpdateProjectHandler { get; }
        public ArchiveProjectHandler ArchiveProjectHandler { get; }

        public static ProjectUseCaseFixture Create(Guid? currentUserId)
        {
            return new ProjectUseCaseFixture(currentUserId);
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

        public Project AddProject(
            Guid? projectId = null,
            Guid? organizationId = null,
            string name = "Launch Control",
            string normalizedName = "LAUNCH CONTROL")
        {
            var project = Project.Create(
                projectId ?? ProjectId,
                organizationId ?? OrganizationId,
                name,
                normalizedName,
                null,
                Now);

            Projects.Items.Add(project);
            return project;
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

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public List<Project> Items { get; } = [];

        public List<(Guid OrganizationId, Guid ProjectId)> GetCalls { get; } = [];

        public List<(Guid OrganizationId, int Page, int PageSize)> ListCalls { get; } = [];

        public List<(Guid OrganizationId, string NormalizedName, Guid? ExcludingProjectId)> ActiveNameChecks { get; } = [];

        public Task AddAsync(Project project, CancellationToken cancellationToken)
        {
            Items.Add(project);
            return Task.CompletedTask;
        }

        public Task<Project?> GetByOrganizationAndIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        {
            GetCalls.Add((organizationId, projectId));
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.Id == projectId));
        }

        public Task<PageResult<Project>> ListPageForOrganizationAsync(
            Guid organizationId,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            ListCalls.Add((organizationId, page, pageSize));
            var items = Items
                .Where(item => item.OrganizationId == organizationId && item.Status == ProjectStatus.Active)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            var totalCount = Items.Count(item => item.OrganizationId == organizationId && item.Status == ProjectStatus.Active);
            return Task.FromResult(new PageResult<Project>(items, page, pageSize, totalCount));
        }

        public Task<bool> ActiveNormalizedNameExistsAsync(
            Guid organizationId,
            string normalizedName,
            Guid? excludingProjectId,
            CancellationToken cancellationToken)
        {
            ActiveNameChecks.Add((organizationId, normalizedName, excludingProjectId));
            var exists = Items.Any(item =>
                item.OrganizationId == organizationId &&
                item.Status == ProjectStatus.Active &&
                item.Id != excludingProjectId &&
                string.Equals(item.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }

    private sealed class FakeProjectAdministratorAssignmentService : IProjectAdministratorAssignmentService
    {
        public List<(Guid ProjectId, Guid CreatorUserId)> Calls { get; } = [];

        public Task AssignCreatorAsProjectAdministratorAsync(Project project, Guid creatorUserId, CancellationToken cancellationToken)
        {
            Calls.Add((project.Id, creatorUserId));
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