namespace PermissionGraph.Application.Features.RoleAssignments.RevokeRoleAssignment.Validators;

public sealed class RevokeRoleAssignmentCommandValidator : AbstractValidator<RevokeRoleAssignmentCommand>
{
    public RevokeRoleAssignmentCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(RoleAssignment.ReasonMinLength)
            .MaximumLength(RoleAssignment.ReasonMaxLength);
    }
}
