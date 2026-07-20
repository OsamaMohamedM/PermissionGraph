using FluentValidation;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class ListOrganizationsQueryValidator : AbstractValidator<ListOrganizationsQuery>
{
    public ListOrganizationsQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Cursor).MaximumLength(500);
    }
}
