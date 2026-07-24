namespace PermissionGraph.Domain.RoleAssignments.Models;

public sealed class RoleAssignment
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 1000;
    public const int HardMaximumTemporaryDurationDays = 365;

    private RoleAssignment(
        Guid id,
        Guid organizationId,
        Guid userId,
        Guid roleId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId,
        RoleAssignmentStatus status,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? expiresAtUtc,
        Guid grantedByUserId,
        string grantReason,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        Status = status;
        StartsAtUtc = startsAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        GrantedByUserId = grantedByUserId;
        GrantReason = grantReason;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private RoleAssignment()
    {
        GrantReason = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public RoleAssignmentScopeType ScopeType { get; private set; }

    public Guid ScopeId { get; private set; }

    public RoleAssignmentStatus Status { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public Guid GrantedByUserId { get; private set; }

    public string GrantReason { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public string? RevokeReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool IsFinal => Status is RoleAssignmentStatus.Revoked or RoleAssignmentStatus.Expired;

    public static RoleAssignment Create(
        Guid id,
        Guid organizationId,
        Guid userId,
        Guid roleId,
        RoleAssignmentScopeType scopeType,
        Guid scopeId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? expiresAtUtc,
        Guid grantedByUserId,
        string grantReason,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureNotEmpty(userId, nameof(userId));
        EnsureNotEmpty(roleId, nameof(roleId));
        EnsureScope(scopeType, organizationId, scopeId);
        EnsureUtc(startsAtUtc, nameof(startsAtUtc));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureOptionalUtc(expiresAtUtc, nameof(expiresAtUtc));
        EnsureTimeWindow(startsAtUtc, expiresAtUtc, createdAtUtc);
        EnsureNotEmpty(grantedByUserId, nameof(grantedByUserId));

        var normalizedGrantReason = NormalizeReason(
            grantReason,
            "role_assignment_grant_reason_required",
            "Grant reason is required.",
            "role_assignment_grant_reason_length",
            "Grant reason length is invalid.");
        var status = startsAtUtc <= createdAtUtc
            ? RoleAssignmentStatus.Active
            : RoleAssignmentStatus.Scheduled;

        return new RoleAssignment(
            id,
            organizationId,
            userId,
            roleId,
            scopeType,
            scopeId,
            status,
            startsAtUtc,
            expiresAtUtc,
            grantedByUserId,
            normalizedGrantReason,
            createdAtUtc);
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAtUtc)
    {
        EnsureUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));

        return Status is RoleAssignmentStatus.Active or RoleAssignmentStatus.Scheduled &&
            StartsAtUtc <= evaluatedAtUtc &&
            !IsExpiredAt(evaluatedAtUtc);
    }

    public bool IsScheduledAt(DateTimeOffset evaluatedAtUtc)
    {
        EnsureUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));

        return Status == RoleAssignmentStatus.Scheduled && evaluatedAtUtc < StartsAtUtc;
    }

    public bool IsExpiredAt(DateTimeOffset evaluatedAtUtc)
    {
        EnsureUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));

        return Status == RoleAssignmentStatus.Expired ||
            (ExpiresAtUtc is not null && evaluatedAtUtc >= ExpiresAtUtc.Value);
    }

    public void Revoke(Guid revokedByUserId, string revokeReason, DateTimeOffset revokedAtUtc)
    {
        EnsureNotEmpty(revokedByUserId, nameof(revokedByUserId));
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));

        if (Status == RoleAssignmentStatus.Revoked)
        {
            throw new DomainRuleViolationException(
                "role_assignment_already_revoked",
                "Revoked role assignment cannot be revoked again.");
        }

        if (Status == RoleAssignmentStatus.Expired)
        {
            throw new DomainRuleViolationException(
                "role_assignment_expired_cannot_be_revoked",
                "Expired role assignment cannot be revoked.");
        }

        RevokeReason = NormalizeReason(
            revokeReason,
            "role_assignment_revoke_reason_required",
            "Revoke reason is required.",
            "role_assignment_revoke_reason_length",
            "Revoke reason length is invalid.");
        RevokedAtUtc = revokedAtUtc;
        RevokedByUserId = revokedByUserId;
        Status = RoleAssignmentStatus.Revoked;
        UpdatedAtUtc = revokedAtUtc;
    }

    public bool Expire(DateTimeOffset expiredAtUtc)
    {
        EnsureUtc(expiredAtUtc, nameof(expiredAtUtc));

        if (Status == RoleAssignmentStatus.Expired)
        {
            return false;
        }

        if (Status == RoleAssignmentStatus.Revoked)
        {
            return false;
        }

        if (ExpiresAtUtc is null || expiredAtUtc < ExpiresAtUtc.Value)
        {
            throw new DomainRuleViolationException(
                "role_assignment_not_expired",
                "Role assignment has not reached expiration.");
        }

        Status = RoleAssignmentStatus.Expired;
        UpdatedAtUtc = expiredAtUtc;
        return true;
    }

    private static void EnsureScope(
        RoleAssignmentScopeType scopeType,
        Guid organizationId,
        Guid scopeId)
    {
        if (!Enum.IsDefined(scopeType))
        {
            throw new DomainRuleViolationException(
                "role_assignment_scope_invalid",
                "Role assignment scope type is invalid.");
        }

        EnsureNotEmpty(scopeId, nameof(scopeId));

        if (scopeType == RoleAssignmentScopeType.Organization && scopeId != organizationId)
        {
            throw new DomainRuleViolationException(
                "role_assignment_organization_scope_mismatch",
                "Organization role assignment scope must match the organization.");
        }
    }

    private static void EnsureTimeWindow(
        DateTimeOffset startsAtUtc,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (expiresAtUtc is null)
        {
            return;
        }

        if (expiresAtUtc.Value <= startsAtUtc)
        {
            throw new DomainRuleViolationException(
                "role_assignment_expiration_before_start",
                "Role assignment expiration must be after the start time.");
        }

        if (expiresAtUtc.Value <= createdAtUtc)
        {
            throw new DomainRuleViolationException(
                "role_assignment_already_expired",
                "Already expired role assignment cannot be created.");
        }

        if (expiresAtUtc.Value - startsAtUtc > TimeSpan.FromDays(HardMaximumTemporaryDurationDays))
        {
            throw new DomainRuleViolationException(
                "role_assignment_temporary_duration_too_long",
                "Temporary role assignment duration is too long.");
        }
    }

    private static string NormalizeReason(
        string? reason,
        string requiredCode,
        string requiredMessage,
        string lengthCode,
        string lengthMessage)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException(requiredCode, requiredMessage);
        }

        var normalized = reason.Trim();
        if (normalized.Length is < ReasonMinLength or > ReasonMaxLength)
        {
            throw new DomainRuleViolationException(lengthCode, lengthMessage);
        }

        return normalized;
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

    private static void EnsureOptionalUtc(DateTimeOffset? value, string parameterName)
    {
        if (value is not null)
        {
            EnsureUtc(value.Value, parameterName);
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default)
        {
            throw new DomainRuleViolationException(
                "timestamp_required",
                $"{parameterName} is required.");
        }

        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleViolationException(
                "timestamp_must_be_utc",
                $"{parameterName} must be UTC.");
        }
    }
}
