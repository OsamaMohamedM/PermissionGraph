using FluentValidation;
using PermissionGraph.Domain.Organizations;

namespace PermissionGraph.Application.Features.Organizations;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Organization.NameMinLength)
            .MaximumLength(Organization.NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(Organization.DescriptionMaxLength);
    }
}

public sealed class GetOrganizationQueryValidator : AbstractValidator<GetOrganizationQuery>
{
    public GetOrganizationQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
    }
}

public sealed class ListOrganizationsQueryValidator : AbstractValidator<ListOrganizationsQuery>
{
    public ListOrganizationsQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Cursor).MaximumLength(500);
    }
}

public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MinimumLength(Organization.NameMinLength)
            .MaximumLength(Organization.NameMaxLength);
        RuleFor(command => command.Description)
            .MaximumLength(Organization.DescriptionMaxLength);
    }
}

public sealed class ArchiveOrganizationCommandValidator : AbstractValidator<ArchiveOrganizationCommand>
{
    public ArchiveOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Confirmation).Equal("ARCHIVE");
    }
}

public sealed class TransferOwnershipCommandValidator : AbstractValidator<TransferOwnershipCommand>
{
    public TransferOwnershipCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.NewOwnerUserId).NotEmpty();
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(200);
    }
}
