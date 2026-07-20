using FluentValidation;

namespace PermissionGraph.Application.Features.Projects;

public sealed class GetProjectQueryValidator : AbstractValidator<GetProjectQuery>
{
    public GetProjectQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.ProjectId).NotEmpty();
    }
}
