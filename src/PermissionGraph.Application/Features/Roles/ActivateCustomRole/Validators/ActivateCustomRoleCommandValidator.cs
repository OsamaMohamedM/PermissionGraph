namespace PermissionGraph.Application.Features.Roles.ActivateCustomRole.Validators;

public sealed class ActivateCustomRoleCommandValidator : AbstractValidator<ActivateCustomRoleCommand>
{
    public ActivateCustomRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}
