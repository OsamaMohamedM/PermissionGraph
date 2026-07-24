namespace PermissionGraph.Application.Features.RoleAssignments.GetRoleAssignment.Validators;

public sealed class GetRoleAssignmentQueryValidator : AbstractValidator<GetRoleAssignmentQuery>
{
    public GetRoleAssignmentQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.AssignmentId).NotEmpty();
    }
}
