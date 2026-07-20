using FluentValidation;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class SuspendOrganizationMemberCommandValidator : AbstractValidator<SuspendOrganizationMemberCommand>
{
    public SuspendOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}
