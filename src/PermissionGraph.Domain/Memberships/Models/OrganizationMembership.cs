namespace PermissionGraph.Domain.Memberships.Models;

public sealed class OrganizationMembership
{
    private OrganizationMembership(
        Guid id,
        Guid organizationId,
        Guid userId,
        DateTimeOffset joinedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Status = MembershipStatus.Active;
        JoinedAtUtc = joinedAtUtc;
        AuthorizationVersion = 1;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private OrganizationMembership()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; }

    public DateTimeOffset? SuspendedAtUtc { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public long AuthorizationVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool IsActive => Status == MembershipStatus.Active;

    public static OrganizationMembership CreateActive(
        Guid id,
        Guid organizationId,
        Guid userId,
        DateTimeOffset joinedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureNotEmpty(userId, nameof(userId));

        return new OrganizationMembership(id, organizationId, userId, joinedAtUtc, createdAtUtc);
    }

    public void Suspend(bool isOwner, DateTimeOffset suspendedAtUtc)
    {
        EnsureOwnerCanChange(isOwner, "owner_membership_cannot_be_suspended", "The organization owner cannot be suspended.");
        EnsureNotRemoved("removed_membership_cannot_be_suspended", "Removed membership cannot be suspended.");

        if (Status == MembershipStatus.Suspended)
        {
            throw new DomainRuleViolationException("membership_already_suspended", "Suspended membership cannot be suspended again.");
        }

        Status = MembershipStatus.Suspended;
        SuspendedAtUtc = suspendedAtUtc;
        UpdatedAtUtc = suspendedAtUtc;
        IncrementAuthorizationVersion();
    }

    public void Reactivate(DateTimeOffset reactivatedAtUtc)
    {
        EnsureNotRemoved("removed_membership_cannot_be_reactivated", "Removed membership cannot be reactivated.");

        if (Status == MembershipStatus.Active)
        {
            throw new DomainRuleViolationException("membership_already_active", "Active membership cannot be reactivated.");
        }

        Status = MembershipStatus.Active;
        SuspendedAtUtc = null;
        UpdatedAtUtc = reactivatedAtUtc;
        IncrementAuthorizationVersion();
    }

    public void Remove(bool isOwner, DateTimeOffset removedAtUtc)
    {
        EnsureOwnerCanChange(isOwner, "owner_membership_cannot_be_removed", "The organization owner cannot be removed.");
        EnsureNotRemoved("removed_membership_cannot_be_removed", "Removed membership cannot be removed again.");

        Status = MembershipStatus.Removed;
        RemovedAtUtc = removedAtUtc;
        UpdatedAtUtc = removedAtUtc;
        IncrementAuthorizationVersion();
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invalid_identifier",
                $"{parameterName} is required.");
        }
    }

    private static void EnsureOwnerCanChange(bool isOwner, string errorCode, string message)
    {
        if (isOwner)
        {
            throw new DomainRuleViolationException(errorCode, message);
        }
    }

    private void EnsureNotRemoved(string errorCode, string message)
    {
        if (Status == MembershipStatus.Removed)
        {
            throw new DomainRuleViolationException(errorCode, message);
        }
    }

    private void IncrementAuthorizationVersion()
    {
        AuthorizationVersion++;
    }
}