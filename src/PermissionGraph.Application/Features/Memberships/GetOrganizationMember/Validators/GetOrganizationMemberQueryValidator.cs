namespace PermissionGraph.Application.Features.Memberships.GetOrganizationMember.Validators;

public sealed class GetOrganizationMemberQueryValidator : AbstractValidator<GetOrganizationMemberQuery>
{
    public GetOrganizationMemberQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}