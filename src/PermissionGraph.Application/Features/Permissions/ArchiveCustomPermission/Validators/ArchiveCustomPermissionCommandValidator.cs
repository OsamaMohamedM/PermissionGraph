namespace PermissionGraph.Application.Features.Permissions.ArchiveCustomPermission.Validators;

public sealed class ArchiveCustomPermissionCommandValidator : AbstractValidator<ArchiveCustomPermissionCommand>
{
    public ArchiveCustomPermissionCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.PermissionId).NotEmpty();
    }
}