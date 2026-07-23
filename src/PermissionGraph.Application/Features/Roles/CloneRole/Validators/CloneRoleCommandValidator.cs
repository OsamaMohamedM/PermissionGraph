namespace PermissionGraph.Application.Features.Roles.CloneRole.Validators;

public sealed class CloneRoleCommandValidator : AbstractValidator<CloneRoleCommand>
{
    public CloneRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.SourceRoleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(Role.NameMinLength, Role.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Role.DescriptionMaxLength);
    }
}
