namespace PermissionGraph.Application.Features.Permissions.GetPermission.Validators;

public sealed class GetPermissionQueryValidator : AbstractValidator<GetPermissionQuery>
{
    public GetPermissionQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.PermissionId).NotEmpty();
    }
}