namespace PermissionGraph.Application.Features.Projects.UpdateProject.Validators;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Project.NameMinLength)
            .MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Project.DescriptionMaxLength);
    }
}