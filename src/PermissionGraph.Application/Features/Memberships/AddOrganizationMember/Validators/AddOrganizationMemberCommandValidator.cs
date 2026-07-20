using FluentValidation;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class AddOrganizationMemberCommandValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}
