namespace PermissionGraph.Application.Features.Memberships.RemoveOrganizationMember.Validators;

public sealed class RemoveOrganizationMemberCommandValidator : AbstractValidator<RemoveOrganizationMemberCommand>
{
    public RemoveOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}