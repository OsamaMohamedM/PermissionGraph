using FluentValidation;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class LeaveOrganizationCommandValidator : AbstractValidator<LeaveOrganizationCommand>
{
    public LeaveOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}
