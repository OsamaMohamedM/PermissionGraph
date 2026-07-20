using FluentValidation;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class CreateCustomPermissionCommandValidator : AbstractValidator<CreateCustomPermissionCommand>
{
    public CreateCustomPermissionCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Key)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.")
            .Must(key => key is null || !key.StartsWith(PermissionDefinition.ReservedPlatformPrefix, StringComparison.Ordinal))
            .WithMessage("Custom permission key cannot use the reserved platform prefix.");
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.DisplayNameMinLength)
            .MaximumLength(PermissionDefinition.DisplayNameMaxLength);
        RuleFor(command => command.Description).MaximumLength(PermissionDefinition.DescriptionMaxLength);
        RuleFor(command => command.Module)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.ModuleMinLength)
            .MaximumLength(PermissionDefinition.ModuleMaxLength);
        RuleFor(command => command.AllowedScopes)
            .Must(Enum.IsDefined)
            .WithMessage("Allowed scope is invalid.");
    }
}
