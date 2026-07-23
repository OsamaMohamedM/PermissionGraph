namespace PermissionGraph.Domain.Authorization.Models;

public static class AuthorizationReasonCode
{
    public const string AllowedOwnerOverride = "ALLOWED_OWNER_OVERRIDE";
    public const string AllowedRolePermissionMatch = "ALLOWED_ROLE_PERMISSION_MATCH";
    public const string DeniedUnauthenticated = "DENIED_UNAUTHENTICATED";
    public const string DeniedActorInactive = "DENIED_ACTOR_INACTIVE";
    public const string DeniedSubjectInactive = "DENIED_SUBJECT_INACTIVE";
    public const string DeniedOrganizationNotFoundOrInactive = "DENIED_ORGANIZATION_NOT_FOUND_OR_INACTIVE";
    public const string DeniedMembershipNotActive = "DENIED_MEMBERSHIP_NOT_ACTIVE";
    public const string DeniedPermissionNotFoundOrInactive = "DENIED_PERMISSION_NOT_FOUND_OR_INACTIVE";
    public const string DeniedProjectNotFoundOrInactive = "DENIED_PROJECT_NOT_FOUND_OR_INACTIVE";
    public const string DeniedProjectOutsideOrganization = "DENIED_PROJECT_OUTSIDE_ORGANIZATION";
    public const string DeniedScopeMismatch = "DENIED_SCOPE_MISMATCH";
    public const string DeniedNoApplicableGrant = "DENIED_NO_APPLICABLE_GRANT";
    public const string DeniedUnsupportedHistoricalTime = "DENIED_UNSUPPORTED_HISTORICAL_TIME";
    public const string DeniedCheckOtherUsersNotAllowed = "DENIED_CHECK_OTHER_USERS_NOT_ALLOWED";

    private static readonly HashSet<string> DefinedCodes = new(StringComparer.Ordinal)
    {
        AllowedOwnerOverride,
        AllowedRolePermissionMatch,
        DeniedUnauthenticated,
        DeniedActorInactive,
        DeniedSubjectInactive,
        DeniedOrganizationNotFoundOrInactive,
        DeniedMembershipNotActive,
        DeniedPermissionNotFoundOrInactive,
        DeniedProjectNotFoundOrInactive,
        DeniedProjectOutsideOrganization,
        DeniedScopeMismatch,
        DeniedNoApplicableGrant,
        DeniedUnsupportedHistoricalTime,
        DeniedCheckOtherUsersNotAllowed
    };

    public static bool IsDefined(string? reasonCode)
    {
        return reasonCode is not null && DefinedCodes.Contains(reasonCode);
    }

    public static bool IsAllowedReason(string reasonCode)
    {
        return reasonCode is AllowedOwnerOverride or AllowedRolePermissionMatch;
    }
}
