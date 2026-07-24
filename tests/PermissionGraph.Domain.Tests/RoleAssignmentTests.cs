namespace PermissionGraph.Domain.Tests;

public sealed class RoleAssignmentTests
{
    private static readonly Guid AssignmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid GrantedByUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid RevokedByUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InitializesActivePermanentOrganizationAssignment()
    {
        var assignment = CreateOrganizationAssignment();

        assignment.Id.Should().Be(AssignmentId);
        assignment.OrganizationId.Should().Be(OrganizationId);
        assignment.UserId.Should().Be(UserId);
        assignment.RoleId.Should().Be(RoleId);
        assignment.ScopeType.Should().Be(RoleAssignmentScopeType.Organization);
        assignment.ScopeId.Should().Be(OrganizationId);
        assignment.Status.Should().Be(RoleAssignmentStatus.Active);
        assignment.StartsAtUtc.Should().Be(Now);
        assignment.ExpiresAtUtc.Should().BeNull();
        assignment.GrantedByUserId.Should().Be(GrantedByUserId);
        assignment.GrantReason.Should().Be("Needed for operations");
        assignment.RevokedAtUtc.Should().BeNull();
        assignment.RevokedByUserId.Should().BeNull();
        assignment.RevokeReason.Should().BeNull();
        assignment.CreatedAtUtc.Should().Be(Now);
        assignment.UpdatedAtUtc.Should().Be(Now);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void Create_InitializesActiveTemporaryOrganizationAssignment()
    {
        var expiresAt = Now.AddDays(7);

        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);

        assignment.Status.Should().Be(RoleAssignmentStatus.Active);
        assignment.ExpiresAtUtc.Should().Be(expiresAt);
        assignment.IsEffectiveAt(Now.AddDays(3)).Should().BeTrue();
    }

    [Fact]
    public void Create_InitializesScheduledAssignment()
    {
        var startsAt = Now.AddHours(2);

        var assignment = CreateOrganizationAssignment(startsAtUtc: startsAt);

        assignment.Status.Should().Be(RoleAssignmentStatus.Scheduled);
        assignment.IsScheduledAt(Now).Should().BeTrue();
        assignment.IsEffectiveAt(Now).Should().BeFalse();
    }

    [Fact]
    public void Create_InitializesProjectScopedAssignment()
    {
        var assignment = RoleAssignment.Create(
            AssignmentId,
            OrganizationId,
            UserId,
            RoleId,
            RoleAssignmentScopeType.Project,
            ProjectId,
            Now,
            null,
            GrantedByUserId,
            "Needed for project",
            Now);

        assignment.ScopeType.Should().Be(RoleAssignmentScopeType.Project);
        assignment.ScopeId.Should().Be(ProjectId);
        assignment.Status.Should().Be(RoleAssignmentStatus.Active);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", "66666666-6666-6666-6666-666666666666")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000", "33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", "66666666-6666-6666-6666-666666666666")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "00000000-0000-0000-0000-000000000000", "44444444-4444-4444-4444-444444444444", "66666666-6666-6666-6666-666666666666")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333", "00000000-0000-0000-0000-000000000000", "66666666-6666-6666-6666-666666666666")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", "00000000-0000-0000-0000-000000000000")]
    public void Create_RejectsEmptyIdentifiers(
        string assignmentId,
        string organizationId,
        string userId,
        string roleId,
        string grantedByUserId)
    {
        var organization = Guid.Parse(organizationId);
        var act = () => RoleAssignment.Create(
            Guid.Parse(assignmentId),
            organization,
            Guid.Parse(userId),
            Guid.Parse(roleId),
            RoleAssignmentScopeType.Organization,
            organization == Guid.Empty ? OrganizationId : organization,
            Now,
            null,
            Guid.Parse(grantedByUserId),
            "Needed for operations",
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Fact]
    public void Create_RejectsOrganizationScopeWithDifferentScopeId()
    {
        var act = () => RoleAssignment.Create(
            AssignmentId,
            OrganizationId,
            UserId,
            RoleId,
            RoleAssignmentScopeType.Organization,
            ProjectId,
            Now,
            null,
            GrantedByUserId,
            "Needed for operations",
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_organization_scope_mismatch");
    }

    [Fact]
    public void Create_RejectsExpirationAtOrBeforeStart()
    {
        var act = () => CreateOrganizationAssignment(expiresAtUtc: Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_expiration_before_start");
    }

    [Fact]
    public void Create_RejectsAlreadyExpiredAssignment()
    {
        var startsAt = Now.AddHours(-2);
        var expiresAt = Now.AddHours(-1);

        var act = () => CreateOrganizationAssignment(startsAtUtc: startsAt, expiresAtUtc: expiresAt);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_already_expired");
    }

    [Fact]
    public void Create_RejectsTemporaryDurationOverHardMaximum()
    {
        var act = () => CreateOrganizationAssignment(expiresAtUtc: Now.AddDays(RoleAssignment.HardMaximumTemporaryDurationDays).AddTicks(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_temporary_duration_too_long");
    }

    [Fact]
    public void IsEffectiveAt_ReturnsTrueDuringActiveValidWindow()
    {
        var assignment = CreateOrganizationAssignment(expiresAtUtc: Now.AddHours(1));

        assignment.IsEffectiveAt(Now.AddMinutes(30)).Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveAt_ReturnsFalseForScheduledAssignmentBeforeStart()
    {
        var assignment = CreateOrganizationAssignment(startsAtUtc: Now.AddHours(1));

        assignment.IsEffectiveAt(Now).Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveAt_AllowsScheduledAssignmentAfterStartWithoutWorkerMutation()
    {
        var assignment = CreateOrganizationAssignment(startsAtUtc: Now.AddHours(1));

        assignment.IsEffectiveAt(Now.AddHours(1)).Should().BeTrue();
        assignment.Status.Should().Be(RoleAssignmentStatus.Scheduled);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void IsEffectiveAt_ReturnsFalseExactlyAtExpiration()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);

        assignment.IsEffectiveAt(expiresAt).Should().BeFalse();
        assignment.IsExpiredAt(expiresAt).Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveAt_ReturnsFalseAfterExpiration()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);

        assignment.IsEffectiveAt(expiresAt.AddTicks(1)).Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveAt_KeepsPermanentAssignmentEffectiveAfterStart()
    {
        var assignment = CreateOrganizationAssignment();

        assignment.IsEffectiveAt(Now.AddDays(30)).Should().BeTrue();
    }

    [Fact]
    public void Revoke_MarksActiveAssignmentRevoked()
    {
        var assignment = CreateOrganizationAssignment();
        var revokedAt = Now.AddHours(1);

        assignment.Revoke(RevokedByUserId, "Access no longer needed", revokedAt);

        assignment.Status.Should().Be(RoleAssignmentStatus.Revoked);
        assignment.RevokedAtUtc.Should().Be(revokedAt);
        assignment.RevokedByUserId.Should().Be(RevokedByUserId);
        assignment.RevokeReason.Should().Be("Access no longer needed");
        assignment.UpdatedAtUtc.Should().Be(revokedAt);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void IsEffectiveAt_ReturnsFalseForRevokedAssignment()
    {
        var assignment = CreateOrganizationAssignment();
        assignment.Revoke(RevokedByUserId, "Access no longer needed", Now.AddHours(1));

        assignment.IsEffectiveAt(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_RejectsRepeatedRevoke()
    {
        var assignment = CreateOrganizationAssignment();
        assignment.Revoke(RevokedByUserId, "Access no longer needed", Now.AddHours(1));

        var act = () => assignment.Revoke(RevokedByUserId, "Access still not needed", Now.AddHours(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_already_revoked");
    }

    [Fact]
    public void Expire_MarksActiveAssignmentExpired()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);

        var changed = assignment.Expire(expiresAt);

        changed.Should().BeTrue();
        assignment.Status.Should().Be(RoleAssignmentStatus.Expired);
        assignment.UpdatedAtUtc.Should().Be(expiresAt);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void IsEffectiveAt_ReturnsFalseForExpiredAssignment()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);
        assignment.Expire(expiresAt);

        assignment.IsEffectiveAt(expiresAt.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void Expire_IsIdempotentForAlreadyExpiredAssignment()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);
        assignment.Expire(expiresAt);
        var updatedAt = assignment.UpdatedAtUtc;

        var changed = assignment.Expire(expiresAt.AddHours(1));

        changed.Should().BeFalse();
        assignment.Status.Should().Be(RoleAssignmentStatus.Expired);
        assignment.UpdatedAtUtc.Should().Be(updatedAt);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void Expire_IsNoOpForRevokedAssignment()
    {
        var expiresAt = Now.AddHours(1);
        var assignment = CreateOrganizationAssignment(expiresAtUtc: expiresAt);
        assignment.Revoke(RevokedByUserId, "Access no longer needed", Now.AddMinutes(30));
        var updatedAt = assignment.UpdatedAtUtc;

        var changed = assignment.Expire(expiresAt);

        changed.Should().BeFalse();
        assignment.Status.Should().Be(RoleAssignmentStatus.Revoked);
        assignment.UpdatedAtUtc.Should().Be(updatedAt);
        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void Expire_RejectsAssignmentThatHasNotReachedExpiration()
    {
        var assignment = CreateOrganizationAssignment(expiresAtUtc: Now.AddHours(1));

        var act = () => assignment.Expire(Now.AddMinutes(30));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_not_expired");
    }

    [Fact]
    public void DomainMethods_DoNotMutateVersion()
    {
        var assignment = CreateOrganizationAssignment(expiresAtUtc: Now.AddHours(1));

        assignment.Version.Should().Be(0);
        assignment.Revoke(RevokedByUserId, "Access no longer needed", Now.AddMinutes(30));

        assignment.Version.Should().Be(0);
    }

    [Fact]
    public void Create_TrimsGrantReasonAndRejectsInvalidLengths()
    {
        var assignment = CreateOrganizationAssignment(grantReason: "  Valid grant reason  ");

        assignment.GrantReason.Should().Be("Valid grant reason");

        var tooShort = () => CreateOrganizationAssignment(grantReason: "abcd");
        tooShort.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_grant_reason_length");

        var tooLong = () => CreateOrganizationAssignment(grantReason: new string('a', RoleAssignment.ReasonMaxLength + 1));
        tooLong.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_grant_reason_length");
    }

    [Fact]
    public void Revoke_TrimsReasonAndRejectsInvalidLengths()
    {
        var assignment = CreateOrganizationAssignment();

        assignment.Revoke(RevokedByUserId, "  Valid revoke reason  ", Now.AddHours(1));
        assignment.RevokeReason.Should().Be("Valid revoke reason");

        var other = CreateOrganizationAssignment();
        var tooShort = () => other.Revoke(RevokedByUserId, "abcd", Now.AddHours(1));
        tooShort.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_assignment_revoke_reason_length");
    }

    [Fact]
    public void Create_RejectsNonUtcTimestamps()
    {
        var act = () => RoleAssignment.Create(
            AssignmentId,
            OrganizationId,
            UserId,
            RoleId,
            RoleAssignmentScopeType.Organization,
            OrganizationId,
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.FromHours(2)),
            null,
            GrantedByUserId,
            "Needed for operations",
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "timestamp_must_be_utc");
    }

    private static RoleAssignment CreateOrganizationAssignment(
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? expiresAtUtc = null,
        string grantReason = "Needed for operations")
    {
        return RoleAssignment.Create(
            AssignmentId,
            OrganizationId,
            UserId,
            RoleId,
            RoleAssignmentScopeType.Organization,
            OrganizationId,
            startsAtUtc ?? Now,
            expiresAtUtc,
            GrantedByUserId,
            grantReason,
            Now);
    }
}
