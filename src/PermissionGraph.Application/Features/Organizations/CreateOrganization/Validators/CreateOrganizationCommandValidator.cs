namespace PermissionGraph.Application.Features.Organizations.CreateOrganization.Validators;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Organization.NameMinLength)
            .MaximumLength(Organization.NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(Organization.DescriptionMaxLength);
    }
}