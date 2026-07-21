namespace PermissionGraph.Application.Features.Projects.GetProject.Validators;

public sealed class GetProjectQueryValidator : AbstractValidator<GetProjectQuery>
{
    public GetProjectQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.ProjectId).NotEmpty();
    }
}