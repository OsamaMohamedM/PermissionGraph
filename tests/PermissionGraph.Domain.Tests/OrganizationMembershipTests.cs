namespace PermissionGraph.Domain.Tests;

public sealed class OrganizationMembershipTests
{
    private static readonly Guid MembershipId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateActive_InitializesActiveMembership()
    {
        var membership = CreateMembership();

        membership.Id.Should().Be(MembershipId);
        membership.OrganizationId.Should().Be(OrganizationId);
        membership.UserId.Should().Be(UserId);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.IsActive.Should().BeTrue();
        membership.AuthorizationVersion.Should().Be(1);
        membership.JoinedAtUtc.Should().Be(Now);
        membership.CreatedAtUtc.Should().Be(Now);
        membership.UpdatedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000", "33333333-3333-3333-3333-333333333333")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "00000000-0000-0000-0000-000000000000")]
    public void CreateActive_RejectsRequiredIdentifiers(string membershipId, string organizationId, string userId)
    {
        var act = () => OrganizationMembership.CreateActive(
            Guid.Parse(membershipId),
            Guid.Parse(organizationId),
            Guid.Parse(userId),
            Now,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Fact]
    public void Suspend_ChangesActiveMembershipToSuspended()
    {
        var membership = CreateMembership();
        var suspendedAt = Now.AddMinutes(5);

        membership.Suspend(isOwner: false, suspendedAt);

        membership.Status.Should().Be(MembershipStatus.Suspended);
        membership.IsActive.Should().BeFalse();
        membership.SuspendedAtUtc.Should().Be(suspendedAt);
        membership.UpdatedAtUtc.Should().Be(suspendedAt);
        membership.AuthorizationVersion.Should().Be(2);
    }

    [Fact]
    public void Suspend_RejectsOwnerMembership()
    {
        var membership = CreateMembership();

        var act = () => membership.Suspend(isOwner: true, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "owner_membership_cannot_be_suspended");
    }

    [Fact]
    public void Suspend_RejectsAlreadySuspendedMembership()
    {
        var membership = CreateMembership();
        membership.Suspend(isOwner: false, Now.AddMinutes(1));

        var act = () => membership.Suspend(isOwner: false, Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "membership_already_suspended");
    }

    [Fact]
    public void Reactivate_ChangesSuspendedMembershipToActive()
    {
        var membership = CreateMembership();
        membership.Suspend(isOwner: false, Now.AddMinutes(1));
        var reactivatedAt = Now.AddMinutes(5);

        membership.Reactivate(reactivatedAt);

        membership.Status.Should().Be(MembershipStatus.Active);
        membership.IsActive.Should().BeTrue();
        membership.SuspendedAtUtc.Should().BeNull();
        membership.UpdatedAtUtc.Should().Be(reactivatedAt);
        membership.AuthorizationVersion.Should().Be(3);
    }

    [Fact]
    public void Reactivate_RejectsActiveMembership()
    {
        var membership = CreateMembership();

        var act = () => membership.Reactivate(Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "membership_already_active");
    }

    [Fact]
    public void Remove_ChangesActiveMembershipToRemoved()
    {
        var membership = CreateMembership();
        var removedAt = Now.AddMinutes(5);

        membership.Remove(isOwner: false, removedAt);

        membership.Status.Should().Be(MembershipStatus.Removed);
        membership.IsActive.Should().BeFalse();
        membership.RemovedAtUtc.Should().Be(removedAt);
        membership.UpdatedAtUtc.Should().Be(removedAt);
        membership.AuthorizationVersion.Should().Be(2);
    }

    [Fact]
    public void Remove_ChangesSuspendedMembershipToRemoved()
    {
        var membership = CreateMembership();
        membership.Suspend(isOwner: false, Now.AddMinutes(1));

        membership.Remove(isOwner: false, Now.AddMinutes(5));

        membership.Status.Should().Be(MembershipStatus.Removed);
        membership.AuthorizationVersion.Should().Be(3);
    }

    [Fact]
    public void Remove_RejectsOwnerMembership()
    {
        var membership = CreateMembership();

        var act = () => membership.Remove(isOwner: true, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "owner_membership_cannot_be_removed");
    }

    [Fact]
    public void RemovedMembership_IsTerminal()
    {
        var membership = CreateMembership();
        membership.Remove(isOwner: false, Now.AddMinutes(1));

        var suspend = () => membership.Suspend(isOwner: false, Now.AddMinutes(2));
        var reactivate = () => membership.Reactivate(Now.AddMinutes(2));
        var remove = () => membership.Remove(isOwner: false, Now.AddMinutes(2));

        suspend.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "removed_membership_cannot_be_suspended");
        reactivate.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "removed_membership_cannot_be_reactivated");
        remove.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "removed_membership_cannot_be_removed");
    }

    private static OrganizationMembership CreateMembership()
    {
        return OrganizationMembership.CreateActive(
            MembershipId,
            OrganizationId,
            UserId,
            Now,
            Now);
    }
}