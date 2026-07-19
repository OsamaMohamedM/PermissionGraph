using FluentValidation;
using PermissionGraph.Api.Endpoints;
using PermissionGraph.Contracts.OrganizationMembers;
using PermissionGraph.Contracts.Organizations;

namespace PermissionGraph.Api.Validation;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(500);
    }
}

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(500);
    }
}

public sealed class ArchiveOrganizationRequestValidator : AbstractValidator<ArchiveOrganizationRequest>
{
    public ArchiveOrganizationRequestValidator()
    {
        RuleFor(request => request.Confirmation).Equal("ARCHIVE");
    }
}

public sealed class TransferOwnershipRequestValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator()
    {
        RuleFor(request => request.NewOwnerUserId).NotEmpty();
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(200);
    }
}

public sealed class ListOrganizationsRequestValidator : AbstractValidator<ListOrganizationsRequest>
{
    public ListOrganizationsRequestValidator()
    {
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(500);
    }
}

public sealed class AddOrganizationMemberRequestValidator : AbstractValidator<AddOrganizationMemberRequest>
{
    public AddOrganizationMemberRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class ListOrganizationMembersRequestValidator : AbstractValidator<ListOrganizationMembersRequest>
{
    public ListOrganizationMembersRequestValidator()
    {
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(500);
        RuleFor(request => request.Search).MaximumLength(200);
        RuleFor(request => request.Status)
            .Must(value => value is null || value.Equals("Active", StringComparison.OrdinalIgnoreCase) || value.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status is invalid.");
    }
}
