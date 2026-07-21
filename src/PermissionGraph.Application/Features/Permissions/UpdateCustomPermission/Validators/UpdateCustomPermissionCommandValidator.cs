namespace PermissionGraph.Application.Features.Permissions.UpdateCustomPermission.Validators;

public sealed class UpdateCustomPermissionCommandValidator : AbstractValidator<UpdateCustomPermissionCommand>
{
    public UpdateCustomPermissionCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.PermissionId).NotEmpty();
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.DisplayNameMinLength)
            .MaximumLength(PermissionDefinition.DisplayNameMaxLength);
        RuleFor(command => command.Description).MaximumLength(PermissionDefinition.DescriptionMaxLength);
        RuleFor(command => command.Module)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.ModuleMinLength)
            .MaximumLength(PermissionDefinition.ModuleMaxLength);
    }
}