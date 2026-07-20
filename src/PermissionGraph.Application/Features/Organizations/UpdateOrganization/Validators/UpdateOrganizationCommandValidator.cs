using FluentValidation;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Organization.NameMinLength)
            .MaximumLength(Organization.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Organization.DescriptionMaxLength);
    }
}
