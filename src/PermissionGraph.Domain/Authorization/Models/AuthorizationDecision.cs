namespace PermissionGraph.Domain.Authorization.Models;

public sealed record AuthorizationDecision
{
    private AuthorizationDecision(
        bool allowed,
        string reasonCode,
        DateTimeOffset evaluatedAtUtc)
    {
        if (!AuthorizationReasonCode.IsDefined(reasonCode))
        {
            throw new DomainRuleViolationException(
                "authorization_reason_invalid",
                "Authorization reason code is invalid.");
        }

        if (evaluatedAtUtc == default)
        {
            throw new DomainRuleViolationException(
                "authorization_evaluated_at_required",
                "Authorization evaluation timestamp is required.");
        }

        Allowed = allowed;
        ReasonCode = reasonCode;
        EvaluatedAtUtc = evaluatedAtUtc;
    }

    public bool Allowed { get; }

    public string ReasonCode { get; }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public static AuthorizationDecision Allow(
        string reasonCode,
        DateTimeOffset evaluatedAtUtc)
    {
        if (!AuthorizationReasonCode.IsAllowedReason(reasonCode))
        {
            throw new DomainRuleViolationException(
                "authorization_allow_reason_invalid",
                "Allowed authorization decisions require an allowed reason code.");
        }

        return new AuthorizationDecision(true, reasonCode, evaluatedAtUtc);
    }

    public static AuthorizationDecision Deny(
        string reasonCode,
        DateTimeOffset evaluatedAtUtc)
    {
        if (AuthorizationReasonCode.IsAllowedReason(reasonCode))
        {
            throw new DomainRuleViolationException(
                "authorization_deny_reason_invalid",
                "Denied authorization decisions require a denied reason code.");
        }

        return new AuthorizationDecision(false, reasonCode, evaluatedAtUtc);
    }
}
