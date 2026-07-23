namespace PermissionGraph.Application.Features.Roles.UpdateCustomRole.Validators;

public sealed class UpdateCustomRoleCommandValidator : AbstractValidator<UpdateCustomRoleCommand>
{
    public UpdateCustomRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(Role.NameMinLength, Role.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Role.DescriptionMaxLength);
    }
}
