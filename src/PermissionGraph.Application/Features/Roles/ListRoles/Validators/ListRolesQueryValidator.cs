namespace PermissionGraph.Application.Features.Roles.ListRoles.Validators;

public sealed class ListRolesQueryValidator : AbstractValidator<ListRolesQuery>
{
    public ListRolesQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.RoleType)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("Role type is invalid.");
        RuleFor(query => query.ScopeType)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("Role scope type is invalid.");
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
