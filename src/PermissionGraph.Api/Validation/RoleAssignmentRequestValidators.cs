namespace PermissionGraph.Api.Validation;

public sealed class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.RoleId).NotEmpty();
        RuleFor(request => request.ScopeType)
            .NotEmpty()
            .Must(BeScopeType)
            .WithMessage("Role assignment scope type is invalid.");
        RuleFor(request => request.ScopeId).NotEmpty();
        RuleFor(request => request.StartsAtUtc).NotEqual(default(DateTimeOffset));
        RuleFor(request => request.ExpiresAtUtc)
            .Must((request, expiresAtUtc) => expiresAtUtc is null || expiresAtUtc > request.StartsAtUtc)
            .WithMessage("Role assignment expiration must be after the start time.");
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MinimumLength(RoleAssignment.ReasonMinLength)
            .MaximumLength(RoleAssignment.ReasonMaxLength);
    }

    private static bool BeScopeType(string? value)
    {
        return Enum.TryParse<RoleAssignmentScopeType>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);
    }
}

public sealed class RevokeRoleAssignmentRequestValidator : AbstractValidator<RevokeRoleAssignmentRequest>
{
    public RevokeRoleAssignmentRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MinimumLength(RoleAssignment.ReasonMinLength)
            .MaximumLength(RoleAssignment.ReasonMaxLength);
    }
}

public sealed class ListRoleAssignmentsRequestValidator : AbstractValidator<ListRoleAssignmentsRequest>
{
    public ListRoleAssignmentsRequestValidator()
    {
        RuleFor(request => request.ScopeType)
            .Must(value => value is null || BeEnumValue<RoleAssignmentScopeType>(value))
            .WithMessage("Role assignment scope type is invalid.");

        RuleFor(request => request.Status)
            .Must(value => value is null || BeEnumValue<RoleAssignmentStatus>(value))
            .WithMessage("Role assignment status is invalid.");

        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);
    }

    private static bool BeEnumValue<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);
    }
}
