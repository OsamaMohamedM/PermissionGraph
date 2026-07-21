namespace PermissionGraph.Application.Features.Memberships.ListOrganizationMembers.Validators;

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