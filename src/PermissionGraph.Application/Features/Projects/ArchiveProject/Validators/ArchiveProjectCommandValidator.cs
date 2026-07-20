using FluentValidation;

namespace PermissionGraph.Application.Features.Projects;

public sealed class ArchiveProjectCommandValidator : AbstractValidator<ArchiveProjectCommand>
{
    public ArchiveProjectCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Confirmation).Equal("ARCHIVE");
    }
}
