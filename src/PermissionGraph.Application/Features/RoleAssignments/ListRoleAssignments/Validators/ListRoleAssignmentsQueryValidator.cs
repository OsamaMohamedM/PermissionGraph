namespace PermissionGraph.Application.Features.RoleAssignments.ListRoleAssignments.Validators;

public sealed class ListRoleAssignmentsQueryValidator : AbstractValidator<ListRoleAssignmentsQuery>
{
    public ListRoleAssignmentsQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.ScopeType)
            .Must(scopeType => scopeType is null || Enum.IsDefined(scopeType.Value))
            .WithMessage("Role assignment scope type is invalid.");
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.IsDefined(status.Value))
            .WithMessage("Role assignment status is invalid.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
