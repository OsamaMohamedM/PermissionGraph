using FluentValidation;
using PermissionGraph.Api.Endpoints;
using PermissionGraph.Contracts.Projects;

namespace PermissionGraph.Api.Validation;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(2000);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(2000);
    }
}

public sealed class ListProjectsRequestValidator : AbstractValidator<ListProjectsRequest>
{
    public ListProjectsRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
    }
}
