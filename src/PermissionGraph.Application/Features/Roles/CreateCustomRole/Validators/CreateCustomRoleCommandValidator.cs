namespace PermissionGraph.Application.Features.Roles.CreateCustomRole.Validators;

public sealed class CreateCustomRoleCommandValidator : AbstractValidator<CreateCustomRoleCommand>
{
    public CreateCustomRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().Length(Role.NameMinLength, Role.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Role.DescriptionMaxLength);
        RuleFor(command => command.ScopeType).Must(Enum.IsDefined).WithMessage("Role scope type is invalid.");
        RuleFor(command => command.PermissionIds).NotNull();
        RuleForEach(command => command.PermissionIds).NotEmpty();
    }
}
