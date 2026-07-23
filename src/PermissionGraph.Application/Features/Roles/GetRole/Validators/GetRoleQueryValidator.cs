namespace PermissionGraph.Application.Features.Roles.GetRole.Validators;

public sealed class GetRoleQueryValidator : AbstractValidator<GetRoleQuery>
{
    public GetRoleQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.RoleId).NotEmpty();
    }
}
