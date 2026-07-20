using FluentValidation;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class GetOrganizationMemberQueryValidator : AbstractValidator<GetOrganizationMemberQuery>
{
    public GetOrganizationMemberQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}
