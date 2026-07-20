using FluentValidation;
using PermissionGraph.Domain.Projects;

namespace PermissionGraph.Application.Features.Projects;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Project.NameMinLength)
            .MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Project.DescriptionMaxLength);
    }
}

public sealed class ListProjectsQueryValidator : AbstractValidator<ListProjectsQuery>
{
    public ListProjectsQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetProjectQueryValidator : AbstractValidator<GetProjectQuery>
{
    public GetProjectQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.ProjectId).NotEmpty();
    }
}

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Project.NameMinLength)
            .MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Project.DescriptionMaxLength);
    }
}

public sealed class ArchiveProjectCommandValidator : AbstractValidator<ArchiveProjectCommand>
{
    public ArchiveProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Confirmation).Equal("ARCHIVE");
    }
}
