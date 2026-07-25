namespace PermissionGraph.Application.Tests;

public sealed class M07RoleAssignmentApplicationTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TargetUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ProjectRoleId = Guid.Parse("55555555-5555-5555-5555-555555555556");
    private static readonly Guid PermissionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProjectPermissionId = Guid.Parse("66666666-6666-6666-6666-666666666667");
    private static readonly Guid ProjectId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid AssignmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OwnerAssignsOrganizationRoleSuccessfully()
    {
        var fixture = new Fixture(currentUserId: OwnerId);

        var result = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        result.Status.Should().Be(RoleAssignmentStatus.Active);
        result.ScopeType.Should().Be(RoleAssignmentScopeType.Organization);
        fixture.Assignments.Items.Should().ContainSingle();
        fixture.Memberships.AuthorizationVersionIncrements.Should().ContainSingle(item => item.UserId == TargetUserId);
        fixture.PolicyVersionIncrements.Should().Be(0);
        fixture.Audit.Records.Should().ContainSingle(record => record.Action == "role_assignment.created");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task OwnerAssignsProjectRoleSuccessfully()
    {
        var fixture = new Fixture(currentUserId: OwnerId);

        var result = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, ProjectRoleId, RoleAssignmentScopeType.Project, ProjectId, Now, null, "Grant project access"),
            CancellationToken.None);

        result.ScopeType.Should().Be(RoleAssignmentScopeType.Project);
        result.ScopeId.Should().Be(ProjectId);
    }

    [Fact]
    public async Task ScheduledAndTemporaryAssignmentsMaterializeDomainState()
    {
        var fixture = new Fixture(currentUserId: OwnerId);

        var scheduled = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now.AddHours(1), null, "Grant later"),
            CancellationToken.None);
        var temporary = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, ActorId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, Now.AddDays(7), "Grant briefly"),
            CancellationToken.None);

        scheduled.Status.Should().Be(RoleAssignmentStatus.Scheduled);
        temporary.ExpiresAtUtc.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task TargetUserMustBeActiveMember()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        fixture.Memberships.Items.RemoveAll(item => item.UserId == TargetUserId);

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>()
            .Where(exception => exception.ErrorCode == "organization_member_not_found");
    }

    [Fact]
    public async Task InactiveRoleCannotBeAssigned()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        fixture.Roles.Items.Single(item => item.Id == RoleId).Archive(Now.AddMinutes(-1));

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_role_inactive");
    }

    [Fact]
    public async Task WrongScopeRoleAssignmentIsRejected()
    {
        var fixture = new Fixture(currentUserId: OwnerId);

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Project, ProjectId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_scope_mismatch");
    }

    [Fact]
    public async Task ProjectOutsideOrganizationIsRejectedAsSafeNotFound()
    {
        var fixture = new Fixture(currentUserId: OwnerId);

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, ProjectRoleId, RoleAssignmentScopeType.Project, Guid.Parse("99999999-9999-9999-9999-999999999999"), Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundApplicationException>()
            .Where(exception => exception.ErrorCode == "project_not_found");
    }

    [Fact]
    public async Task DuplicateEffectiveAssignmentIsRejected()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        fixture.Assignments.Items.Add(CreateAssignment(TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId));

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_duplicate_effective");
    }

    [Fact]
    public async Task NonOwnerSelfAssignmentIsDeniedAndAudited()
    {
        var fixture = new Fixture(currentUserId: ActorId);
        fixture.Authorization.Allow("pg.roles.assign");
        fixture.Authorization.Allow("documents.manage");

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, ActorId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_self_assignment_denied");
        fixture.Audit.Records.Should().Contain(record => record.Action == "role_assignment.privilege_escalation_denied");
        fixture.Assignments.Items.Should().BeEmpty();
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task NonOwnerAssigningRoleBeyondOwnPermissionsIsDenied()
    {
        var fixture = new Fixture(currentUserId: ActorId);
        fixture.Authorization.Allow("pg.roles.assign");

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_grantability_denied");
        fixture.Audit.Records.Should().Contain(record => record.Action == "role_assignment.privilege_escalation_denied");
        fixture.Assignments.Items.Should().BeEmpty();
        fixture.Permissions.BatchVisibleLookupCalls.Should().Be(1);
        fixture.Authorization.BatchQueries.Should().ContainSingle();
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task NonOwnerAssigningRoleWithinOwnPermissionsIsAllowed()
    {
        var fixture = new Fixture(currentUserId: ActorId);
        fixture.Authorization.Allow("pg.roles.assign");
        fixture.Authorization.Allow("documents.manage");

        var result = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        result.Id.Should().Be(AssignmentId);
        fixture.Permissions.BatchVisibleLookupCalls.Should().Be(1);
        fixture.Authorization.CheckQueries.Should().ContainSingle(query => query.PermissionKey == "pg.roles.assign");
        fixture.Authorization.BatchQueries.Should().ContainSingle();
        fixture.Authorization.BatchQueries.Single().Checks.Should().ContainSingle(item => item.PermissionKey == "documents.manage");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task NonOwnerAssigningProjectRoleWithinOwnPermissionsUsesBatchGrantability()
    {
        var fixture = new Fixture(currentUserId: ActorId);
        fixture.Authorization.Allow("pg.roles.assign");
        fixture.Authorization.Allow("documents.review");

        var result = await fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, ProjectRoleId, RoleAssignmentScopeType.Project, ProjectId, Now, null, "Grant project access"),
            CancellationToken.None);

        result.Id.Should().Be(AssignmentId);
        result.ScopeType.Should().Be(RoleAssignmentScopeType.Project);
        fixture.Permissions.BatchVisibleLookupCalls.Should().Be(1);
        fixture.Authorization.CheckQueries.Should().ContainSingle(query => query.PermissionKey == "pg.roles.assign" && query.ProjectId == ProjectId);
        fixture.Authorization.BatchQueries.Should().ContainSingle();
        fixture.Authorization.BatchQueries.Single().Checks.Should().HaveCount(2);
        fixture.Authorization.BatchQueries.Single().Checks.Should().Contain(item => item.PermissionKey == "documents.review" && item.ProjectId == ProjectId);
        fixture.Authorization.BatchQueries.Single().Checks.Should().Contain(item => item.PermissionKey == "documents.review" && item.ProjectId == null);
    }

    [Fact]
    public async Task RevokeSuccessIncrementsVersionAuditsAndCommits()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        var assignment = CreateAssignment(TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId);
        fixture.Assignments.Items.Add(assignment);

        var result = await fixture.Revoke.HandleAsync(
            new RevokeRoleAssignmentCommand(OrganizationId, assignment.Id, "Access removed"),
            CancellationToken.None);

        result.Status.Should().Be(RoleAssignmentStatus.Revoked);
        fixture.Memberships.AuthorizationVersionIncrements.Should().ContainSingle(item => item.UserId == TargetUserId);
        fixture.PolicyVersionIncrements.Should().Be(0);
        fixture.Audit.Records.Should().ContainSingle(record => record.Action == "role_assignment.revoked");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task RepeatedRevokeConflicts()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        var assignment = CreateAssignment(TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId);
        assignment.Revoke(OwnerId, "Access removed", Now.AddMinutes(1));
        fixture.Assignments.Items.Add(assignment);

        var act = () => fixture.Revoke.HandleAsync(
            new RevokeRoleAssignmentCommand(OrganizationId, assignment.Id, "Access removed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_already_revoked");
    }

    [Fact]
    public async Task NonOwnerCannotRevokeOwnAssignment()
    {
        var fixture = new Fixture(currentUserId: ActorId);
        var assignment = CreateAssignment(ActorId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId);
        fixture.Assignments.Items.Add(assignment);
        fixture.Authorization.Allow("pg.roles.assign");

        var act = () => fixture.Revoke.HandleAsync(
            new RevokeRoleAssignmentCommand(OrganizationId, assignment.Id, "Access removed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_self_revoke_denied");
    }

    [Fact]
    public async Task ExpireCommandIncrementsVersionAuditsAndDoesNotIncrementPolicyVersion()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        var assignment = CreateAssignment(TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, expiresAtUtc: Now.AddMinutes(-1));
        fixture.Assignments.Items.Add(assignment);

        var result = await fixture.Expire.HandleAsync(new ExpireRoleAssignmentsCommand(Now, 100), CancellationToken.None);

        result.ExpiredCount.Should().Be(1);
        fixture.Memberships.AuthorizationVersionIncrements.Should().ContainSingle(item => item.UserId == TargetUserId);
        fixture.PolicyVersionIncrements.Should().Be(0);
        fixture.Audit.Records.Should().ContainSingle(record => record.Action == "role_assignment.expired");
        fixture.Transaction.CommitCalls.Should().Be(1);
    }

    [Fact]
    public async Task AuditFailurePreventsCommit()
    {
        var fixture = new Fixture(currentUserId: OwnerId);
        fixture.Audit.ThrowOnWrite = true;

        var act = () => fixture.Assign.HandleAsync(
            new AssignRoleCommand(OrganizationId, TargetUserId, RoleId, RoleAssignmentScopeType.Organization, OrganizationId, Now, null, "Grant access"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fixture.Transaction.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthorizationDecisionServiceAllowsActiveRoleAssignmentPath()
    {
        var fixture = new AuthorizationFixture();
        fixture.Read.RoleAssignmentPaths.Add(AuthorizationFixture.RolePath(RoleAssignmentStatus.Active, Now.AddMinutes(-1), null));

        var decision = await fixture.Service.CheckAsync(
            new CheckPermissionQuery(null, OrganizationId, null, "documents.manage"),
            CancellationToken.None);

        decision.ShouldBeAllowed(AuthorizationReasonCode.AllowedRolePermissionMatch);
    }

    [Fact]
    public async Task AuthorizationDecisionServiceDeniesScheduledBeforeStartExpiredAndRevokedAssignments()
    {
        var fixture = new AuthorizationFixture();

        foreach (var path in new[]
        {
            AuthorizationFixture.RolePath(RoleAssignmentStatus.Scheduled, Now.AddMinutes(1), null),
            AuthorizationFixture.RolePath(RoleAssignmentStatus.Active, Now.AddMinutes(-2), Now),
            AuthorizationFixture.RolePath(RoleAssignmentStatus.Revoked, Now.AddMinutes(-2), null)
        })
        {
            fixture.Read.RoleAssignmentPaths.Clear();
            fixture.Read.RoleAssignmentPaths.Add(path);

            var decision = await fixture.Service.CheckAsync(
                new CheckPermissionQuery(null, OrganizationId, null, "documents.manage"),
                CancellationToken.None);

            decision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
        }
    }

    [Fact]
    public async Task AuthorizationDecisionServiceDeniesInactiveRoleOrPermissionPath()
    {
        var fixture = new AuthorizationFixture();
        fixture.Read.RoleAssignmentPaths.Add(AuthorizationFixture.RolePath(RoleAssignmentStatus.Active, Now.AddMinutes(-1), null, roleActive: false));

        var inactiveRole = await fixture.Service.CheckAsync(new CheckPermissionQuery(null, OrganizationId, null, "documents.manage"), CancellationToken.None);
        inactiveRole.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);

        fixture.Read.RoleAssignmentPaths.Clear();
        fixture.Read.RoleAssignmentPaths.Add(AuthorizationFixture.RolePath(RoleAssignmentStatus.Active, Now.AddMinutes(-1), null, permissionActive: false));

        var inactivePermission = await fixture.Service.CheckAsync(new CheckPermissionQuery(null, OrganizationId, null, "documents.manage"), CancellationToken.None);
        inactivePermission.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task AuthorizationDecisionServiceHonorsAssignmentScopesAndCompatibilityPath()
    {
        var fixture = new AuthorizationFixture(projectId: ProjectId);
        fixture.Read.RoleAssignmentPaths.Add(AuthorizationFixture.RolePath(RoleAssignmentStatus.Active, Now.AddMinutes(-1), null, RoleAssignmentScopeType.Project, Guid.Parse("99999999-9999-9999-9999-999999999999")));

        var wrongProject = await fixture.Service.CheckAsync(new CheckPermissionQuery(null, OrganizationId, ProjectId, "documents.manage"), CancellationToken.None);
        wrongProject.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);

        fixture.Read.RoleAssignmentPaths.Clear();
        fixture.Read.ProjectAdministratorPaths.Add(AuthorizationFixture.ProjectAdminPath());

        var compatibility = await fixture.Service.CheckAsync(new CheckPermissionQuery(null, OrganizationId, ProjectId, "documents.manage"), CancellationToken.None);
        compatibility.ShouldBeAllowed(AuthorizationReasonCode.AllowedRolePermissionMatch);
    }

    [Fact]
    public void ApplicationEvaluatorUsesCacheAbstractionWithoutRedisDependency()
    {
        var constructorTypes = typeof(AuthorizationDecisionService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        constructorTypes.Should().NotContain(type => type.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        constructorTypes.Should().Contain(typeof(IAuthorizationDecisionCache).FullName);
    }

    private static RoleAssignment CreateAssignment(
        Guid userId,
        Guid roleId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId,
        DateTimeOffset? expiresAtUtc = null)
    {
        return RoleAssignment.Create(
            AssignmentId,
            OrganizationId,
            userId,
            roleId,
            scopeType,
            scopeId,
            Now.AddMinutes(-10),
            expiresAtUtc,
            OwnerId,
            "Initial grant",
            Now.AddMinutes(-10));
    }

    private sealed class Fixture
    {
        public Fixture(Guid currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Organizations = new FakeOrganizationRepository();
            Memberships = new FakeMembershipRepository();
            Projects = new FakeProjectRepository();
            Permissions = new FakePermissionRepository();
            Roles = new FakeRoleRepository();
            Assignments = new FakeRoleAssignmentRepository();
            Authorization = new FakeAuthorizationDecisionService();
            Audit = new FakeAuditWriter();
            Transaction = new FakeApplicationTransaction();
            Ids = new FakeGuidProvider();
            Clock = new FakeClock(Now);

            Users.Add(OwnerId);
            Users.Add(ActorId);
            Users.Add(TargetUserId);
            Organizations.Items.Add(Organization.Create(OrganizationId, "Example Org", "EXAMPLE ORG", null, OwnerId, Now.AddDays(-1)));
            Memberships.Items.Add(OrganizationMembership.CreateActive(Guid.NewGuid(), OrganizationId, OwnerId, Now.AddDays(-1), Now.AddDays(-1)));
            Memberships.Items.Add(OrganizationMembership.CreateActive(Guid.NewGuid(), OrganizationId, ActorId, Now.AddDays(-1), Now.AddDays(-1)));
            Memberships.Items.Add(OrganizationMembership.CreateActive(Guid.NewGuid(), OrganizationId, TargetUserId, Now.AddDays(-1), Now.AddDays(-1)));
            Projects.Items.Add(Project.Create(ProjectId, OrganizationId, "Project", "PROJECT", null, Now.AddDays(-1)));
            Permissions.Items.Add(OrganizationPermission());
            Permissions.Items.Add(ProjectPermission());
            Roles.Items.Add(Role.CreateCustom(RoleId, OrganizationId, "Manager", "MANAGER", null, RoleScopeType.Organization, false, Now.AddDays(-1), [OrganizationPermission()], OwnerId));
            Roles.Items.Add(Role.CreateCustom(ProjectRoleId, OrganizationId, "Project Manager", "PROJECT MANAGER", null, RoleScopeType.Project, false, Now.AddDays(-1), [ProjectPermission()], OwnerId));

            var resolver = new AuthenticatedUserResolver(CurrentUser, Users);
            Assign = new AssignRoleHandler(
                new AssignRoleCommandValidator(),
                resolver,
                Organizations,
                Memberships,
                Projects,
                Roles,
                Permissions,
                Assignments,
                Authorization,
                Audit,
                Transaction,
                Ids,
                Clock);
            Revoke = new RevokeRoleAssignmentHandler(
                new RevokeRoleAssignmentCommandValidator(),
                resolver,
                Organizations,
                Assignments,
                Authorization,
                Memberships,
                Audit,
                Transaction,
                Clock);
            Expire = new ExpireRoleAssignmentsHandler(
                new ExpireRoleAssignmentsCommandValidator(),
                Assignments,
                Memberships,
                Audit,
                Transaction);
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeOrganizationRepository Organizations { get; }
        public FakeMembershipRepository Memberships { get; }
        public FakeProjectRepository Projects { get; }
        public FakePermissionRepository Permissions { get; }
        public FakeRoleRepository Roles { get; }
        public FakeRoleAssignmentRepository Assignments { get; }
        public FakeAuthorizationDecisionService Authorization { get; }
        public FakeAuditWriter Audit { get; }
        public FakeApplicationTransaction Transaction { get; }
        public FakeGuidProvider Ids { get; }
        public FakeClock Clock { get; }
        public int PolicyVersionIncrements { get; } = 0;
        public AssignRoleHandler Assign { get; }
        public RevokeRoleAssignmentHandler Revoke { get; }
        public ExpireRoleAssignmentsHandler Expire { get; }
    }

    private sealed class AuthorizationFixture
    {
        public AuthorizationFixture(Guid? projectId = null)
        {
            CurrentUser = new FakeCurrentUser(ActorId);
            Users = new FakeUserAccountLookup();
            Users.Add(ActorId);
            Read = new FakeAuthorizationReadService(projectId);
            Service = new AuthorizationDecisionService(
                new CheckPermissionQueryValidator(),
                new BatchCheckPermissionsQueryValidator(),
                CurrentUser,
                Users,
                Read,
                new NoOpAuthorizationDecisionCache(),
                new FakeClock(Now));
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeAuthorizationReadService Read { get; }
        public AuthorizationDecisionService Service { get; }

        public static RoleAssignmentPermissionPathReadModel RolePath(
            RoleAssignmentStatus status,
            DateTimeOffset startsAtUtc,
            DateTimeOffset? expiresAtUtc,
            RoleAssignmentScopeType scopeType = RoleAssignmentScopeType.Organization,
            Guid? scopeId = null,
            bool roleActive = true,
            bool permissionActive = true)
        {
            return new RoleAssignmentPermissionPathReadModel(
                OrganizationId,
                ActorId,
                RoleId,
                scopeType,
                scopeId ?? OrganizationId,
                status,
                startsAtUtc,
                expiresAtUtc,
                roleActive,
                scopeType == RoleAssignmentScopeType.Organization ? RoleScopeType.Organization : RoleScopeType.Project,
                PermissionId,
                "documents.manage",
                PermissionAllowedScopes.OrganizationAndProject,
                permissionActive);
        }

        public static ProjectAdministratorPermissionPathReadModel ProjectAdminPath()
        {
            return new ProjectAdministratorPermissionPathReadModel(
                OrganizationId,
                ProjectId,
                ActorId,
                ProjectRoleId,
                true,
                RoleScopeType.Project,
                PermissionId,
                "documents.manage",
                PermissionAllowedScopes.OrganizationAndProject,
                true);
        }
    }

    private static PermissionDefinition OrganizationPermission()
    {
        return PermissionDefinition.CreateCustom(
            PermissionId,
            OrganizationId,
            "documents.manage",
            "documents.manage",
            "Manage documents",
            null,
            "Documents",
            PermissionAllowedScopes.OrganizationAndProject,
            false,
            Now.AddDays(-1));
    }

    private static PermissionDefinition ProjectPermission()
    {
        return PermissionDefinition.CreateCustom(
            ProjectPermissionId,
            OrganizationId,
            "documents.review",
            "documents.review",
            "Review documents",
            null,
            "Documents",
            PermissionAllowedScopes.Project,
            false,
            Now.AddDays(-1));
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class FakeUserAccountLookup : IUserAccountLookup
    {
        private readonly Dictionary<Guid, UserAccount> _users = [];

        public void Add(Guid userId, bool isActive = true)
        {
            _users[userId] = new UserAccount(userId, $"{userId:N}@example.test", "User", isActive);
        }

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(_users.Values.SingleOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
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

    private sealed class FakeMembershipRepository : IOrganizationMembershipRepository
    {
        public List<OrganizationMembership> Items { get; } = [];
        public List<(Guid OrganizationId, Guid UserId)> AuthorizationVersionIncrements { get; } = [];

        public Task AddAsync(OrganizationMembership membership, CancellationToken cancellationToken)
        {
            Items.Add(membership);
            return Task.CompletedTask;
        }

        public Task<OrganizationMembership?> GetByOrganizationAndUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.UserId == userId && item.IsActive));
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
            AuthorizationVersionIncrements.Add((organizationId, userId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public List<Project> Items { get; } = [];

        public Task AddAsync(Project project, CancellationToken cancellationToken)
        {
            Items.Add(project);
            return Task.CompletedTask;
        }

        public Task<Project?> GetByOrganizationAndIdAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.Id == projectId));
        }

        public Task<PageResult<Project>> ListPageForOrganizationAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PageResult<Project>([], page, pageSize, 0));
        }

        public Task<bool> ActiveNormalizedNameExistsAsync(Guid organizationId, string normalizedName, Guid? excludingProjectId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakePermissionRepository : IPermissionDefinitionRepository
    {
        public List<PermissionDefinition> Items { get; } = [];
        public int BatchVisibleLookupCalls { get; private set; }

        public Task AddAsync(PermissionDefinition permission, CancellationToken cancellationToken)
        {
            Items.Add(permission);
            return Task.CompletedTask;
        }

        public Task<PermissionDefinition?> GetVisibleByOrganizationAndIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == permissionId && (item.PermissionType == PermissionType.Platform || item.OrganizationId == organizationId)));
        }

        public Task<IReadOnlyList<PermissionDefinition>> ListVisibleByOrganizationAndIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
        {
            BatchVisibleLookupCalls++;
            return Task.FromResult<IReadOnlyList<PermissionDefinition>>(Items
                .Where(item =>
                    permissionIds.Contains(item.Id) &&
                    (item.PermissionType == PermissionType.Platform || item.OrganizationId == organizationId))
                .ToArray());
        }

        public Task<PermissionDefinition?> GetOrganizationCustomByIdAsync(Guid organizationId, Guid permissionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == permissionId && item.OrganizationId == organizationId));
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
            return Task.FromResult(new PageResult<Role>([], page, pageSize, 0));
        }

        public Task<bool> ActiveNormalizedNameExistsAsync(Guid organizationId, RoleScopeType scopeType, string normalizedName, Guid? excludingRoleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeRoleAssignmentRepository : IRoleAssignmentRepository
    {
        public List<RoleAssignment> Items { get; } = [];

        public Task AddAsync(RoleAssignment assignment, CancellationToken cancellationToken)
        {
            Items.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<RoleAssignment?> GetVisibleByOrganizationAndIdAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.OrganizationId == organizationId && item.Id == assignmentId));
        }

        public Task<RoleAssignment?> GetByOrganizationAndIdForMutationAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
        {
            return GetVisibleByOrganizationAndIdAsync(organizationId, assignmentId, cancellationToken);
        }

        public Task<PageResult<RoleAssignment>> ListVisibleForOrganizationAsync(Guid organizationId, RoleAssignmentListFilters filters, int page, int pageSize, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PageResult<RoleAssignment>(Items.Where(item => item.OrganizationId == organizationId).ToArray(), page, pageSize, Items.Count));
        }

        public Task<bool> HasEffectiveAssignmentAsync(Guid organizationId, Guid userId, Guid roleId, RoleAssignmentScopeType scopeType, Guid scopeId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Any(item =>
                item.OrganizationId == organizationId &&
                item.UserId == userId &&
                item.RoleId == roleId &&
                item.ScopeType == scopeType &&
                item.ScopeId == scopeId &&
                item.Status is RoleAssignmentStatus.Active or RoleAssignmentStatus.Scheduled));
        }

        public Task<IReadOnlyList<RoleAssignment>> ListExpiredForUpdateAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RoleAssignment>>(Items
                .Where(item => item.ExpiresAtUtc is not null && item.ExpiresAtUtc <= nowUtc && item.Status is RoleAssignmentStatus.Active or RoleAssignmentStatus.Scheduled)
                .Take(batchSize)
                .ToArray());
        }
    }

    private sealed class FakeAuthorizationDecisionService : IAuthorizationDecisionService
    {
        private readonly HashSet<string> _allowedPermissions = new(StringComparer.Ordinal);

        public List<CheckPermissionQuery> CheckQueries { get; } = [];
        public List<BatchCheckPermissionsQuery> BatchQueries { get; } = [];

        public void Allow(string permissionKey)
        {
            _allowedPermissions.Add(permissionKey);
        }

        public Task<AuthorizationDecision> CheckAsync(CheckPermissionQuery query, CancellationToken cancellationToken)
        {
            CheckQueries.Add(query);
            var decision = _allowedPermissions.Contains(query.PermissionKey)
                ? AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedOwnerOverride, Now)
                : AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);
            return Task.FromResult(decision);
        }

        public Task<BatchAuthorizationDecisionResult> BatchCheckAsync(BatchCheckPermissionsQuery query, CancellationToken cancellationToken)
        {
            BatchQueries.Add(query);
            var decisions = query.OrderedChecks
                .Select((item, index) =>
                {
                    var decision = _allowedPermissions.Contains(item.PermissionKey)
                        ? AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedOwnerOverride, Now)
                        : AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);
                    return new BatchAuthorizationDecision(item.CorrelationId, index, decision);
                })
                .ToArray();

            return Task.FromResult(new BatchAuthorizationDecisionResult(decisions));
        }
    }

    private sealed class FakeAuthorizationReadService(Guid? projectId) : IAuthorizationReadService
    {
        public List<RoleAssignmentPermissionPathReadModel> RoleAssignmentPaths { get; } = [];
        public List<ProjectAdministratorPermissionPathReadModel> ProjectAdministratorPaths { get; } = [];

        public Task<AuthorizationEvaluationReadModel> LoadEvaluationAsync(AuthorizationEvaluationReadRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Build(request));
        }

        public Task<IReadOnlyList<AuthorizationEvaluationReadModel>> LoadBatchEvaluationAsync(IReadOnlyList<AuthorizationEvaluationReadRequest> requests, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AuthorizationEvaluationReadModel>>(requests.Select(Build).ToArray());
        }

        private AuthorizationEvaluationReadModel Build(AuthorizationEvaluationReadRequest request)
        {
            return new AuthorizationEvaluationReadModel(
                request,
                new AuthorizationOrganizationReadModel(OrganizationId, OwnerId, true),
                new AuthorizationPermissionReadModel(PermissionId, OrganizationId, "documents.manage", PermissionType.Custom, PermissionAllowedScopes.OrganizationAndProject, true),
                projectId is null ? null : new AuthorizationProjectReadModel(projectId.Value, OrganizationId, true),
                new AuthorizationMembershipReadModel(OrganizationId, ActorId, true),
                RoleAssignmentPaths,
                ProjectAdministratorPaths);
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
        public int CommitCalls { get; private set; }

        public Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
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
        public Guid NewGuid()
        {
            return AssignmentId;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
