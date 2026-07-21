namespace PermissionGraph.Application.Features.Memberships.ReactivateOrganizationMember.Validators;

public sealed class ReactivateOrganizationMemberCommandValidator : AbstractValidator<ReactivateOrganizationMemberCommand>
{
    public ReactivateOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}