using FluentValidation;
using PermissionGraph.Domain.Memberships;

namespace PermissionGraph.Application.Features.Memberships;

public sealed class AddOrganizationMemberCommandValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class GetOrganizationMemberQueryValidator : AbstractValidator<GetOrganizationMemberQuery>
{
    public GetOrganizationMemberQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}

public sealed class ListOrganizationMembersQueryValidator : AbstractValidator<ListOrganizationMembersQuery>
{
    public ListOrganizationMembersQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Cursor).MaximumLength(500);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Status)
            .Must(value => value is null || Enum.TryParse<MembershipStatus>(value, ignoreCase: true, out _))
            .WithMessage("Status is invalid.");
    }
}

public sealed class SuspendOrganizationMemberCommandValidator : AbstractValidator<SuspendOrganizationMemberCommand>
{
    public SuspendOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}

public sealed class ReactivateOrganizationMemberCommandValidator : AbstractValidator<ReactivateOrganizationMemberCommand>
{
    public ReactivateOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}

public sealed class RemoveOrganizationMemberCommandValidator : AbstractValidator<RemoveOrganizationMemberCommand>
{
    public RemoveOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}

public sealed class LeaveOrganizationCommandValidator : AbstractValidator<LeaveOrganizationCommand>
{
    public LeaveOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}
