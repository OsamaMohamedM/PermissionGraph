using FluentValidation;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class GetOrganizationQueryValidator : AbstractValidator<GetOrganizationQuery>
{
    public GetOrganizationQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
    }
}
