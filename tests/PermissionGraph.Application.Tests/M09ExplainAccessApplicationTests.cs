namespace PermissionGraph.Application.Tests;

public sealed class M09ExplainAccessApplicationTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SubjectId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid PermissionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProjectId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SelfExplainReturnsNormalDecisionAndSafeSteps()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Next = AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedRolePermissionMatch, Now);
        fixture.Read.Model = ReadModel(subjectUserId: ActorId, ownerUserId: OwnerId, roleAssignments:
        [
            MatchingAssignment(ActorId, RoleAssignmentScopeType.Organization, OrganizationId)
        ]);

        var result = await fixture.Handler.HandleAsync(
            new ExplainAccessQuery(null, OrganizationId, null, "documents.review"),
            CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedRolePermissionMatch);
        result.SubjectUserId.Should().Be(ActorId);
        result.MatchedPath?.Type.Should().Be("RoleAssignment");
        result.Steps.Should().Contain(step => step.Code == "ROLE_ASSIGNMENT_MATCHED" && step.Status == AccessExplanationStepStatus.Passed);
        fixture.Decisions.LastQuery.Should().Be(new CheckPermissionQuery(null, OrganizationId, null, "documents.review"));
    }

    [Fact]
    public async Task OwnerExplainOtherUserIsAllowedAuditedAndPreservesDecision()
    {
        var fixture = new Fixture(OwnerId);
        fixture.Decisions.Next = AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedOwnerOverride, Now);
        fixture.Read.Model = ReadModel(subjectUserId: SubjectId, ownerUserId: OwnerId);

        var result = await fixture.Handler.HandleAsync(
            new ExplainAccessQuery(SubjectId, OrganizationId, null, "pg.projects.view"),
            CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.ReasonCode.Should().Be(AuthorizationReasonCode.AllowedOwnerOverride);
        result.ActorUserId.Should().Be(OwnerId);
        result.SubjectUserId.Should().Be(SubjectId);
        result.MatchedPath?.Type.Should().Be("OwnerOverride");
        fixture.Audit.Records.Should().Contain(record => record.Action == "authorization.explain_other" && record.Result == "Succeeded");
    }

    [Fact]
    public async Task NonOwnerWithExplainOthersPermissionCanExplainOtherUser()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Enqueue(
            AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedRolePermissionMatch, Now),
            AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now));
        fixture.Read.Model = ReadModel(subjectUserId: SubjectId, ownerUserId: OwnerId);

        var result = await fixture.Handler.HandleAsync(
            new ExplainAccessQuery(SubjectId, OrganizationId, null, "pg.projects.view"),
            CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
        fixture.Decisions.Queries.Should().Equal(
            new CheckPermissionQuery(null, OrganizationId, null, "pg.authorization.explain_others"),
            new CheckPermissionQuery(SubjectId, OrganizationId, null, "pg.projects.view"));
        fixture.Audit.Records.Should().Contain(record => record.Action == "authorization.explain_other" && record.Result == "Succeeded");
    }

    [Fact]
    public async Task NonOwnerExplainOtherUserWithoutExplainOthersPermissionIsDeniedBeforeTargetDecisionDisclosure()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Next = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);
        fixture.Read.Model = ReadModel(subjectUserId: SubjectId, ownerUserId: OwnerId);

        var act = () => fixture.Handler.HandleAsync(
            new ExplainAccessQuery(SubjectId, OrganizationId, null, "pg.projects.view"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "access_explanation_other_user_denied");
        fixture.Decisions.Queries.Should().Equal(
            new CheckPermissionQuery(null, OrganizationId, null, "pg.authorization.explain_others"));
        fixture.Audit.Records.Should().Contain(record => record.Result == "Failed");
    }

    [Fact]
    public async Task ExplainOtherDoesNotLeakCrossTenantDataWhenActorLacksExplainOthersPermission()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Next = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive, Now);
        fixture.Read.Model = ReadModel(subjectUserId: SubjectId, ownerUserId: OwnerId) with { Organization = null };

        var act = () => fixture.Handler.HandleAsync(
            new ExplainAccessQuery(SubjectId, OrganizationId, null, "pg.projects.view"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenApplicationException>()
            .Where(exception => exception.ErrorCode == "access_explanation_other_user_denied");
        fixture.Decisions.Queries.Should().ContainSingle();
    }

    [Fact]
    public async Task DeniedNoGrantIncludesNoApplicableGrantFinalStep()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Next = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);
        fixture.Read.Model = ReadModel(subjectUserId: ActorId, ownerUserId: OwnerId);

        var result = await fixture.Handler.HandleAsync(
            new ExplainAccessQuery(null, OrganizationId, null, "documents.review"),
            CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.ReasonCode.Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
        result.MatchedPath.Should().BeNull();
        result.Steps.Last().Details["reasonCode"].Should().Be(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task ScheduledExpiredRevokedAndInactiveRoleAssignmentsAreExplained()
    {
        var fixture = new Fixture(ActorId);
        fixture.Decisions.Next = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);
        fixture.Read.Model = ReadModel(subjectUserId: ActorId, ownerUserId: OwnerId, roleAssignments:
        [
            MatchingAssignment(ActorId, RoleAssignmentScopeType.Organization, OrganizationId) with { AssignmentStartsAtUtc = Now.AddHours(1) },
            MatchingAssignment(ActorId, RoleAssignmentScopeType.Organization, OrganizationId) with { AssignmentExpiresAtUtc = Now.AddSeconds(-1) },
            MatchingAssignment(ActorId, RoleAssignmentScopeType.Organization, OrganizationId) with { AssignmentStatus = RoleAssignmentStatus.Revoked },
            MatchingAssignment(ActorId, RoleAssignmentScopeType.Organization, OrganizationId) with { RoleIsActive = false }
        ]);

        var result = await fixture.Handler.HandleAsync(
            new ExplainAccessQuery(null, OrganizationId, null, "documents.review"),
            CancellationToken.None);

        result.Steps.Select(step => step.Code).Should().Contain([
            "ROLE_ASSIGNMENT_NOT_STARTED",
            "ROLE_ASSIGNMENT_EXPIRED",
            "ROLE_ASSIGNMENT_REVOKED",
            "ROLE_INACTIVE"
        ]);
    }

    [Fact]
    public async Task HistoricalEvaluationIsRejected()
    {
        var fixture = new Fixture(ActorId);

        var act = () => fixture.Handler.HandleAsync(
            new ExplainAccessQuery(null, OrganizationId, null, "documents.review", Now.AddMinutes(-1)),
            CancellationToken.None);

        await act.Should().ThrowAsync<CommandValidationException>();
    }

    private static AccessExplanationReadModel ReadModel(
        Guid subjectUserId,
        Guid ownerUserId,
        IReadOnlyList<AccessExplanationRoleAssignmentReadModel>? roleAssignments = null)
    {
        return new AccessExplanationReadModel(
            new AccessExplanationReadRequest(subjectUserId, OrganizationId, null, "documents.review"),
            new AuthorizationOrganizationReadModel(OrganizationId, ownerUserId, true),
            new AuthorizationPermissionReadModel(PermissionId, OrganizationId, "documents.review", PermissionType.Custom, PermissionAllowedScopes.Organization, true),
            null,
            new AuthorizationMembershipReadModel(OrganizationId, subjectUserId, true),
            roleAssignments ?? [],
            []);
    }

    private static AccessExplanationRoleAssignmentReadModel MatchingAssignment(
        Guid subjectUserId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId)
    {
        return new AccessExplanationRoleAssignmentReadModel(
            AssignmentId: Guid.NewGuid(),
            AssignmentOrganizationId: OrganizationId,
            AssignmentUserId: subjectUserId,
            AssignmentRoleId: RoleId,
            AssignmentScopeType: scopeType,
            AssignmentScopeId: scopeId,
            AssignmentStatus: RoleAssignmentStatus.Active,
            AssignmentStartsAtUtc: Now.AddHours(-1),
            AssignmentExpiresAtUtc: null,
            RoleName: "Reviewers",
            RoleIsActive: true,
            RoleScopeType: scopeType == RoleAssignmentScopeType.Organization ? RoleScopeType.Organization : RoleScopeType.Project,
            RoleContainsPermission: true,
            MatchedPermissionId: PermissionId,
            MatchedPermissionNormalizedKey: "documents.review",
            MatchedPermissionAllowedScopes: PermissionAllowedScopes.Organization,
            MatchedPermissionIsActive: true);
    }

    private sealed class Fixture
    {
        public Fixture(Guid currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            Users.Items[currentUserId] = new UserAccount(currentUserId, $"{currentUserId}@example.test", "Actor", true);
            Users.Items[OwnerId] = new UserAccount(OwnerId, "owner@example.test", "Owner", true);
            Users.Items[SubjectId] = new UserAccount(SubjectId, "subject@example.test", "Subject", true);
            Decisions = new FakeAuthorizationDecisionService();
            Read = new FakeAccessExplanationReadService();
            Transaction = new FakeApplicationTransaction();
            Audit = new FakeAuditWriter();
            Handler = new ExplainAccessHandler(
                new ExplainAccessQueryValidator(),
                CurrentUser,
                Users,
                Decisions,
                Read,
                Transaction,
                Audit,
                new FakeClock(Now));
        }

        public FakeCurrentUser CurrentUser { get; }

        public FakeUserAccountLookup Users { get; }

        public FakeAuthorizationDecisionService Decisions { get; }

        public FakeAccessExplanationReadService Read { get; }

        public FakeApplicationTransaction Transaction { get; }

        public FakeAuditWriter Audit { get; }

        public ExplainAccessHandler Handler { get; }
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class FakeUserAccountLookup : IUserAccountLookup
    {
        public Dictionary<Guid, UserAccount> Items { get; } = [];

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            Items.TryGetValue(userId, out var user);
            return Task.FromResult<UserAccount?>(user);
        }

        public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult<UserAccount?>(Items.Values.SingleOrDefault(item => item.Email == email));
        }
    }

    private sealed class FakeAuthorizationDecisionService : IAuthorizationDecisionService
    {
        private readonly Queue<AuthorizationDecision> decisions = new();

        public AuthorizationDecision Next { get; set; } = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);

        public CheckPermissionQuery? LastQuery { get; private set; }

        public List<CheckPermissionQuery> Queries { get; } = [];

        public void Enqueue(params AuthorizationDecision[] items)
        {
            foreach (var item in items)
            {
                decisions.Enqueue(item);
            }
        }

        public Task<AuthorizationDecision> CheckAsync(CheckPermissionQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            Queries.Add(query);
            return Task.FromResult(decisions.Count == 0 ? Next : decisions.Dequeue());
        }

        public Task<BatchAuthorizationDecisionResult> BatchCheckAsync(BatchCheckPermissionsQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAccessExplanationReadService : IAccessExplanationReadService
    {
        public AccessExplanationReadModel Model { get; set; } = ReadModel(ActorId, OwnerId);

        public Task<AccessExplanationReadModel> LoadAsync(AccessExplanationReadRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Model with { Request = request });
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
        public int CommitCount { get; private set; }

        public Task<IApplicationTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IApplicationTransactionScope>(new Scope(this));
        }

        private sealed class Scope(FakeApplicationTransaction owner) : IApplicationTransactionScope
        {
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCount++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
