namespace PermissionGraph.Application.Features.RoleAssignments.AssignRole.Validators;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.ScopeType)
            .Must(scopeType => Enum.IsDefined(scopeType))
            .WithMessage("Role assignment scope type is invalid.");
        RuleFor(command => command.ScopeId).NotEmpty();
        RuleFor(command => command.StartsAtUtc).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.ExpiresAtUtc)
            .Must((command, expiresAtUtc) => expiresAtUtc is null || expiresAtUtc > command.StartsAtUtc)
            .WithMessage("Role assignment expiration must be after the start time.");
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(RoleAssignment.ReasonMinLength)
            .MaximumLength(RoleAssignment.ReasonMaxLength);
    }
}
