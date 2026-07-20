using FluentValidation;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class ActivateCustomPermissionCommandValidator : AbstractValidator<ActivateCustomPermissionCommand>
{
    public ActivateCustomPermissionCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.PermissionId).NotEmpty();
    }
}
