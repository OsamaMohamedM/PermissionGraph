namespace PermissionGraph.Application.Features.Organizations.ArchiveOrganization.Validators;

public sealed class ArchiveOrganizationCommandValidator : AbstractValidator<ArchiveOrganizationCommand>
{
    public ArchiveOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Confirmation).Equal("ARCHIVE");
    }
}