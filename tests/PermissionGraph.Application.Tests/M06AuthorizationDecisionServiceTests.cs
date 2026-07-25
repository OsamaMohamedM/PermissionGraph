using System.Reflection;

namespace PermissionGraph.Application.Tests;

public sealed class M06AuthorizationDecisionServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SubjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherProjectId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PermissionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid RoleId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 10, 15, 0, TimeSpan.Zero);
    private const string PermissionKey = "pg.projects.view";

    [Fact]
    public async Task Check_DeniesUnauthenticatedActor()
    {
        var fixture = AuthorizationDecisionFixture.Create(null);

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.ShouldBeDenied(AuthorizationReasonCode.DeniedUnauthenticated);
    }

    [Fact]
    public async Task Check_DeniesInactiveActor()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.Users.Accounts[ActorId] = fixture.Users.Accounts[ActorId] with { IsActive = false };

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.ShouldBeDenied(AuthorizationReasonCode.DeniedActorInactive);
    }

    [Fact]
    public async Task Check_DeniesMissingOrInactiveSubject()
    {
        var missing = AuthorizationDecisionFixture.Create(ActorId);
        missing.AddBaseData();

        var missingDecision = await missing.Service.CheckAsync(
            DefaultProjectQuery(subjectUserId: SubjectId),
            CancellationToken.None);

        missingDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedSubjectInactive);

        var inactive = AuthorizationDecisionFixture.Create(ActorId);
        inactive.AddBaseData();
        inactive.AddUser(SubjectId, isActive: false);

        var inactiveDecision = await inactive.Service.CheckAsync(
            DefaultProjectQuery(subjectUserId: SubjectId),
            CancellationToken.None);

        inactiveDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedSubjectInactive);
    }

    [Fact]
    public async Task Check_DeniesMissingOrInactiveOrganization()
    {
        var missing = AuthorizationDecisionFixture.Create(ActorId);
        missing.AddPermission();

        var missingDecision = await missing.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        missingDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive);

        var inactive = AuthorizationDecisionFixture.Create(ActorId);
        inactive.AddBaseData(organizationActive: false);

        var inactiveDecision = await inactive.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactiveDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive);
    }

    [Fact]
    public async Task Check_DeniesMissingOrInactivePermission()
    {
        var missing = AuthorizationDecisionFixture.Create(ActorId);
        missing.AddOrganization();
        missing.AddProject();

        var missingDecision = await missing.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        missingDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);

        var inactive = AuthorizationDecisionFixture.Create(ActorId);
        inactive.AddBaseData(permissionActive: false);

        var inactiveDecision = await inactive.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactiveDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);
    }

    [Fact]
    public async Task Check_DeniesMissingInactiveOrCrossTenantProject()
    {
        var missing = AuthorizationDecisionFixture.Create(ActorId);
        missing.AddOrganization();
        missing.AddPermission();

        var missingDecision = await missing.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        missingDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedProjectNotFoundOrInactive);

        var inactive = AuthorizationDecisionFixture.Create(ActorId);
        inactive.AddBaseData(projectActive: false);

        var inactiveDecision = await inactive.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactiveDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedProjectNotFoundOrInactive);

        var crossTenant = AuthorizationDecisionFixture.Create(ActorId);
        crossTenant.AddBaseData(projectOrganizationId: OtherOrganizationId);

        var crossTenantDecision = await crossTenant.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        crossTenantDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedProjectOutsideOrganization);
    }

    [Fact]
    public async Task Check_DeniesPermissionScopeMismatch()
    {
        var organizationPermissionOnProject = AuthorizationDecisionFixture.Create(ActorId);
        organizationPermissionOnProject.AddBaseData(permissionAllowedScopes: PermissionAllowedScopes.Organization);

        var projectDecision = await organizationPermissionOnProject.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        projectDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedScopeMismatch);

        var projectPermissionAtOrganization = AuthorizationDecisionFixture.Create(ActorId);
        projectPermissionAtOrganization.AddBaseData(permissionAllowedScopes: PermissionAllowedScopes.Project);

        var organizationDecision = await projectPermissionAtOrganization.Service.CheckAsync(DefaultOrganizationQuery(), CancellationToken.None);

        organizationDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedScopeMismatch);
    }

    [Fact]
    public async Task Check_DeniesSuspendedRemovedOrNonMember()
    {
        foreach (var membership in new bool?[] { null, false })
        {
            var fixture = AuthorizationDecisionFixture.Create(ActorId);
            fixture.AddBaseData();
            if (membership is not null)
            {
                fixture.AddMembership(ActorId, membership.Value);
            }

            var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

            decision.ShouldBeDenied(AuthorizationReasonCode.DeniedMembershipNotActive);
        }
    }

    [Fact]
    public async Task Check_AllowsOwnerOverrideAfterPrerequisites()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData(ownerUserId: ActorId);

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.ShouldBeAllowed(AuthorizationReasonCode.AllowedOwnerOverride);
    }

    [Fact]
    public async Task Check_OwnerStillDeniedWhenPermissionInactiveOrProjectInvalid()
    {
        var inactivePermission = AuthorizationDecisionFixture.Create(ActorId);
        inactivePermission.AddBaseData(ownerUserId: ActorId, permissionActive: false);

        var inactiveDecision = await inactivePermission.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactiveDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive);

        var invalidProject = AuthorizationDecisionFixture.Create(ActorId);
        invalidProject.AddBaseData(ownerUserId: ActorId, projectOrganizationId: OtherOrganizationId);

        var invalidProjectDecision = await invalidProject.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        invalidProjectDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedProjectOutsideOrganization);
    }

    [Fact]
    public async Task Check_DeniesActiveMemberWithNoGrant()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData(ownerUserId: Guid.Parse("88888888-8888-8888-8888-888888888888"));
        fixture.AddMembership(ActorId);

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task Check_AllowsProjectAdministratorAssignmentWithMatchingRolePermission()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData();
        fixture.AddMembership(ActorId);
        fixture.AddProjectAdministratorPath(ActorId, ProjectId);

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.ShouldBeAllowed(AuthorizationReasonCode.AllowedRolePermissionMatch);
    }

    [Fact]
    public async Task Check_ProjectAdministratorPathMustMatchProjectRoleAndPermission()
    {
        var anotherProject = AuthorizationDecisionFixture.Create(ActorId);
        anotherProject.AddBaseData();
        anotherProject.AddMembership(ActorId);
        anotherProject.AddProjectAdministratorPath(ActorId, OtherProjectId);

        var anotherProjectDecision = await anotherProject.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        anotherProjectDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);

        var inactiveRole = AuthorizationDecisionFixture.Create(ActorId);
        inactiveRole.AddBaseData();
        inactiveRole.AddMembership(ActorId);
        inactiveRole.AddProjectAdministratorPath(ActorId, ProjectId, roleIsActive: false);

        var inactiveRoleDecision = await inactiveRole.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactiveRoleDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);

        var missingPermission = AuthorizationDecisionFixture.Create(ActorId);
        missingPermission.AddBaseData();
        missingPermission.AddMembership(ActorId);
        missingPermission.AddProjectAdministratorPath(ActorId, ProjectId, permissionId: Guid.NewGuid());

        var missingPermissionDecision = await missingPermission.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        missingPermissionDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);

        var inactivePathPermission = AuthorizationDecisionFixture.Create(ActorId);
        inactivePathPermission.AddBaseData();
        inactivePathPermission.AddMembership(ActorId);
        inactivePathPermission.AddProjectAdministratorPath(ActorId, ProjectId, permissionIsActive: false);

        var inactivePathPermissionDecision = await inactivePathPermission.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        inactivePathPermissionDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task Check_OrganizationScopeDoesNotUseProjectAdministratorPath()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData(permissionAllowedScopes: PermissionAllowedScopes.OrganizationAndProject);
        fixture.AddMembership(ActorId);
        fixture.AddProjectAdministratorPath(ActorId, ProjectId);

        var decision = await fixture.Service.CheckAsync(DefaultOrganizationQuery(), CancellationToken.None);

        decision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task Check_OtherUserDeniedForNonOwnerAndContinuesForOwner()
    {
        var nonOwner = AuthorizationDecisionFixture.Create(ActorId);
        nonOwner.AddBaseData();
        nonOwner.AddUser(SubjectId);
        nonOwner.AddMembership(SubjectId);

        var nonOwnerDecision = await nonOwner.Service.CheckAsync(
            DefaultProjectQuery(subjectUserId: SubjectId),
            CancellationToken.None);

        nonOwnerDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedCheckOtherUsersNotAllowed);

        var owner = AuthorizationDecisionFixture.Create(ActorId);
        owner.AddBaseData(ownerUserId: ActorId);
        owner.AddUser(SubjectId);
        owner.AddMembership(SubjectId);

        var ownerDecision = await owner.Service.CheckAsync(
            DefaultProjectQuery(subjectUserId: SubjectId),
            CancellationToken.None);

        ownerDecision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
    }

    [Fact]
    public async Task Check_OtherUserContinuesForNonOwnerWithExplainOthersPermission()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData(ownerUserId: Guid.Parse("88888888-8888-8888-8888-888888888888"));
        fixture.AddUser(SubjectId);
        fixture.AddMembership(ActorId);
        fixture.AddMembership(SubjectId);
        fixture.AddPermission(
            normalizedKey: "pg.authorization.explain_others",
            permissionId: Guid.Parse("77777777-7777-7777-7777-777777777777"));
        fixture.AddProjectAdministratorPath(
            ActorId,
            ProjectId,
            permissionId: Guid.Parse("77777777-7777-7777-7777-777777777777"),
            permissionKey: "pg.authorization.explain_others");

        var decision = await fixture.Service.CheckAsync(
            DefaultProjectQuery(subjectUserId: SubjectId),
            CancellationToken.None);

        decision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
        fixture.ReadService.LoadSingleCalls.Should().Be(2);
    }

    [Fact]
    public async Task Batch_ReturnsOneDecisionPerItemAndPreservesOrderCorrelationAndMixedResults()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData();
        fixture.AddMembership(ActorId);
        fixture.AddProjectAdministratorPath(ActorId, ProjectId);

        var result = await fixture.Service.BatchCheckAsync(
            new BatchCheckPermissionsQuery(
            [
                new BatchCheckPermissionItem("allow", null, OrganizationId, ProjectId, PermissionKey),
                new BatchCheckPermissionItem("deny", null, OrganizationId, null, PermissionKey)
            ]),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.CorrelationId).Should().Equal("allow", "deny");
        result.Items.Select(item => item.Index).Should().Equal(0, 1);
        result.Items[0].Decision.ShouldBeAllowed(AuthorizationReasonCode.AllowedRolePermissionMatch);
        result.Items[1].Decision.ShouldBeDenied(AuthorizationReasonCode.DeniedNoApplicableGrant);
        fixture.ReadService.LoadBatchCalls.Should().Be(1);
        fixture.ReadService.LoadSingleCalls.Should().Be(0);
    }

    [Fact]
    public async Task Check_UsesClockForEvaluatedAtUtc()
    {
        var fixture = AuthorizationDecisionFixture.Create(ActorId);
        fixture.AddBaseData(ownerUserId: ActorId);

        var decision = await fixture.Service.CheckAsync(DefaultProjectQuery(), CancellationToken.None);

        decision.EvaluatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Evaluator_DoesNotDependOnInfrastructureEfOrRedis()
    {
        var parameterTypes = typeof(AuthorizationDecisionService)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        parameterTypes.Should().NotContain(type => type.Contains("Infrastructure", StringComparison.Ordinal));
        parameterTypes.Should().NotContain(type => type.Contains("EntityFramework", StringComparison.Ordinal));
        parameterTypes.Should().NotContain(type => type.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        parameterTypes.Should().Contain(typeof(IAuthorizationDecisionCache).FullName);
    }

    private static CheckPermissionQuery DefaultProjectQuery(Guid? subjectUserId = null)
    {
        return new CheckPermissionQuery(subjectUserId, OrganizationId, ProjectId, PermissionKey);
    }

    private static CheckPermissionQuery DefaultOrganizationQuery(Guid? subjectUserId = null)
    {
        return new CheckPermissionQuery(subjectUserId, OrganizationId, null, PermissionKey);
    }

    private sealed class AuthorizationDecisionFixture
    {
        private AuthorizationDecisionFixture(Guid? currentUserId)
        {
            CurrentUser = new FakeCurrentUser(currentUserId);
            Users = new FakeUserAccountLookup();
            ReadService = new FakeAuthorizationReadService();
            Clock = new FakeClock(Now);
            Service = new AuthorizationDecisionService(
                new CheckPermissionQueryValidator(),
                new BatchCheckPermissionsQueryValidator(),
                CurrentUser,
                Users,
                ReadService,
                new NoOpAuthorizationDecisionCache(),
                Clock);

            if (currentUserId is not null)
            {
                AddUser(currentUserId.Value);
            }
        }

        public FakeCurrentUser CurrentUser { get; }
        public FakeUserAccountLookup Users { get; }
        public FakeAuthorizationReadService ReadService { get; }
        public FakeClock Clock { get; }
        public AuthorizationDecisionService Service { get; }

        public static AuthorizationDecisionFixture Create(Guid? currentUserId)
        {
            return new AuthorizationDecisionFixture(currentUserId);
        }

        public void AddUser(Guid userId, bool isActive = true)
        {
            Users.Accounts[userId] = new UserAccount(
                userId,
                $"{userId}@example.test",
                "User",
                isActive);
        }

        public void AddBaseData(
            Guid? ownerUserId = null,
            bool organizationActive = true,
            bool permissionActive = true,
            bool projectActive = true,
            Guid? projectOrganizationId = null,
            PermissionAllowedScopes permissionAllowedScopes = PermissionAllowedScopes.OrganizationAndProject)
        {
            AddOrganization(ownerUserId, organizationActive);
            AddPermission(permissionActive: permissionActive, allowedScopes: permissionAllowedScopes);
            AddProject(projectActive, projectOrganizationId);
        }

        public void AddOrganization(Guid? ownerUserId = null, bool isActive = true)
        {
            ReadService.Organizations[OrganizationId] = new AuthorizationOrganizationReadModel(
                OrganizationId,
                ownerUserId ?? SubjectId,
                isActive);
        }

        public void AddPermission(
            bool permissionActive = true,
            PermissionAllowedScopes allowedScopes = PermissionAllowedScopes.OrganizationAndProject,
            string normalizedKey = PermissionKey,
            Guid? permissionId = null)
        {
            ReadService.Permissions[(OrganizationId, normalizedKey)] = new AuthorizationPermissionReadModel(
                permissionId ?? PermissionId,
                null,
                normalizedKey,
                PermissionType.Platform,
                allowedScopes,
                permissionActive);
        }

        public void AddProject(bool isActive = true, Guid? organizationId = null)
        {
            ReadService.Projects[ProjectId] = new AuthorizationProjectReadModel(
                ProjectId,
                organizationId ?? OrganizationId,
                isActive);
        }

        public void AddMembership(Guid userId, bool isActive = true)
        {
            ReadService.Memberships[(OrganizationId, userId)] = new AuthorizationMembershipReadModel(
                OrganizationId,
                userId,
                isActive);
        }

        public void AddProjectAdministratorPath(
            Guid userId,
            Guid projectId,
            bool roleIsActive = true,
            Guid? permissionId = null,
            bool permissionIsActive = true,
            string permissionKey = PermissionKey)
        {
            ReadService.ProjectAdministratorPaths.Add(new ProjectAdministratorPermissionPathReadModel(
                OrganizationId,
                projectId,
                userId,
                RoleId,
                roleIsActive,
                RoleScopeType.Project,
                permissionId ?? PermissionId,
                permissionKey,
                PermissionAllowedScopes.OrganizationAndProject,
                permissionIsActive));
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

    private sealed class FakeAuthorizationReadService : IAuthorizationReadService
    {
        public Dictionary<Guid, AuthorizationOrganizationReadModel> Organizations { get; } = [];
        public Dictionary<(Guid OrganizationId, string NormalizedPermissionKey), AuthorizationPermissionReadModel> Permissions { get; } = [];
        public Dictionary<Guid, AuthorizationProjectReadModel> Projects { get; } = [];
        public Dictionary<(Guid OrganizationId, Guid UserId), AuthorizationMembershipReadModel> Memberships { get; } = [];
        public List<ProjectAdministratorPermissionPathReadModel> ProjectAdministratorPaths { get; } = [];
        public int LoadSingleCalls { get; private set; }
        public int LoadBatchCalls { get; private set; }

        public Task<AuthorizationEvaluationReadModel> LoadEvaluationAsync(
            AuthorizationEvaluationReadRequest request,
            CancellationToken cancellationToken)
        {
            LoadSingleCalls++;
            return Task.FromResult(BuildReadModel(request));
        }

        public Task<IReadOnlyList<AuthorizationEvaluationReadModel>> LoadBatchEvaluationAsync(
            IReadOnlyList<AuthorizationEvaluationReadRequest> requests,
            CancellationToken cancellationToken)
        {
            LoadBatchCalls++;
            var models = requests.Select(BuildReadModel).ToArray();
            return Task.FromResult<IReadOnlyList<AuthorizationEvaluationReadModel>>(models);
        }

        private AuthorizationEvaluationReadModel BuildReadModel(AuthorizationEvaluationReadRequest request)
        {
            Organizations.TryGetValue(request.OrganizationId, out var organization);
            Permissions.TryGetValue((request.OrganizationId, request.NormalizedPermissionKey), out var permission);
            var project = request.ProjectId is null || !Projects.TryGetValue(request.ProjectId.Value, out var foundProject)
                ? null
                : foundProject;
            Memberships.TryGetValue((request.OrganizationId, request.SubjectUserId), out var membership);

            var projectAdministratorPaths = ProjectAdministratorPaths
                .Where(path =>
                    path.AssignmentOrganizationId == request.OrganizationId &&
                    path.AssignmentUserId == request.SubjectUserId &&
                    path.PermissionNormalizedKey == request.NormalizedPermissionKey)
                .ToArray();

            return new AuthorizationEvaluationReadModel(
                request,
                organization,
                permission,
                project,
                membership,
                [],
                projectAdministratorPaths);
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

internal static class AuthorizationDecisionAssertionExtensions
{
    public static void ShouldBeDenied(this AuthorizationDecision decision, string reasonCode)
    {
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be(reasonCode);
    }

    public static void ShouldBeAllowed(this AuthorizationDecision decision, string reasonCode)
    {
        decision.Allowed.Should().BeTrue();
        decision.ReasonCode.Should().Be(reasonCode);
    }
}
