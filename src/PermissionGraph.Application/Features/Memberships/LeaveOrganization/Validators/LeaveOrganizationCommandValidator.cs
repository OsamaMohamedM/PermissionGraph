namespace PermissionGraph.Application.Features.Memberships.LeaveOrganization.Validators;

public sealed class LeaveOrganizationCommandValidator : AbstractValidator<LeaveOrganizationCommand>
{
    public LeaveOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}