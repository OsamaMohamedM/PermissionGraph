namespace PermissionGraph.Application.Features.Organizations.GetOrganization.Validators;

public sealed class GetOrganizationQueryValidator : AbstractValidator<GetOrganizationQuery>
{
    public GetOrganizationQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
    }
}