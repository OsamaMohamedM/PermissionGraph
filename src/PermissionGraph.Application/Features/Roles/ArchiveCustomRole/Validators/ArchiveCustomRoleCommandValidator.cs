namespace PermissionGraph.Application.Features.Roles.ArchiveCustomRole.Validators;

public sealed class ArchiveCustomRoleCommandValidator : AbstractValidator<ArchiveCustomRoleCommand>
{
    public ArchiveCustomRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}
