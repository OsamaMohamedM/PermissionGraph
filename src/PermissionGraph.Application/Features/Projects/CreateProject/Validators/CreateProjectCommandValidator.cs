namespace PermissionGraph.Application.Features.Projects.CreateProject.Validators;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Project.NameMinLength)
            .MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Project.DescriptionMaxLength);
    }
}