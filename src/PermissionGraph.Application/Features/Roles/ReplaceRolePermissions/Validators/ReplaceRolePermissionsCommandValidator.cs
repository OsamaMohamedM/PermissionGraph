namespace PermissionGraph.Application.Features.Roles.ReplaceRolePermissions.Validators;

public sealed class ReplaceRolePermissionsCommandValidator : AbstractValidator<ReplaceRolePermissionsCommand>
{
    public ReplaceRolePermissionsCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotNull();
        RuleForEach(command => command.PermissionIds).NotEmpty();
    }
}
