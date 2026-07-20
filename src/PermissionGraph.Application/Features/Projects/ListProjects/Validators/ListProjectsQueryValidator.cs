using FluentValidation;

namespace PermissionGraph.Application.Features.Projects;

public sealed class ListProjectsQueryValidator : AbstractValidator<ListProjectsQuery>
{
    public ListProjectsQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
