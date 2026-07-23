namespace PermissionGraph.Domain.Tests;

public sealed class AuthorizationModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AuthorizationDecision_CreatesAllowedResult()
    {
        var decision = AuthorizationDecision.Allow(AuthorizationReasonCode.AllowedOwnerOverride, Now);

        decision.Allowed.Should().BeTrue();
        decision.ReasonCode.Should().Be("ALLOWED_OWNER_OVERRIDE");
        decision.EvaluatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void AuthorizationDecision_CreatesDeniedResult()
    {
        var decision = AuthorizationDecision.Deny(AuthorizationReasonCode.DeniedNoApplicableGrant, Now);

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("DENIED_NO_APPLICABLE_GRANT");
        decision.EvaluatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void AuthorizationReasonCode_ContainsStableM06ReasonValues()
    {
        string[] reasonCodes =
        [
            AuthorizationReasonCode.AllowedOwnerOverride,
            AuthorizationReasonCode.AllowedRolePermissionMatch,
            AuthorizationReasonCode.DeniedUnauthenticated,
            AuthorizationReasonCode.DeniedActorInactive,
            AuthorizationReasonCode.DeniedSubjectInactive,
            AuthorizationReasonCode.DeniedOrganizationNotFoundOrInactive,
            AuthorizationReasonCode.DeniedMembershipNotActive,
            AuthorizationReasonCode.DeniedPermissionNotFoundOrInactive,
            AuthorizationReasonCode.DeniedProjectNotFoundOrInactive,
            AuthorizationReasonCode.DeniedProjectOutsideOrganization,
            AuthorizationReasonCode.DeniedScopeMismatch,
            AuthorizationReasonCode.DeniedNoApplicableGrant,
            AuthorizationReasonCode.DeniedUnsupportedHistoricalTime,
            AuthorizationReasonCode.DeniedCheckOtherUsersNotAllowed
        ];

        reasonCodes.Should().OnlyContain(code => AuthorizationReasonCode.IsDefined(code));
        reasonCodes.Should().OnlyContain(code => code == code.ToUpperInvariant());
    }

    [Fact]
    public void AuthorizationScope_CreatesOrganizationOnlyScope()
    {
        var scope = new AuthorizationScope(OrganizationId);

        scope.OrganizationId.Should().Be(OrganizationId);
        scope.ProjectId.Should().BeNull();
        scope.ScopeType.Should().Be(AuthorizationScopeType.Organization);
    }

    [Fact]
    public void AuthorizationScope_CreatesProjectScope()
    {
        var scope = new AuthorizationScope(OrganizationId, ProjectId);

        scope.OrganizationId.Should().Be(OrganizationId);
        scope.ProjectId.Should().Be(ProjectId);
        scope.ScopeType.Should().Be(AuthorizationScopeType.Project);
    }

    [Fact]
    public void AuthorizationScope_RejectsEmptyOrganization()
    {
        var act = () => new AuthorizationScope(Guid.Empty);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "authorization_organization_required");
    }
}
