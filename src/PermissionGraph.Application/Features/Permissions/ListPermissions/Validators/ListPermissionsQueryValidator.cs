using FluentValidation;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Application.Features.Permissions;

public sealed class ListPermissionsQueryValidator : AbstractValidator<ListPermissionsQuery>
{
    public ListPermissionsQueryValidator()
    {
        RuleFor(query => query.OrganizationId).NotEmpty();
        RuleFor(query => query.PermissionType)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("Permission type is invalid.");
        RuleFor(query => query.Module).MaximumLength(PermissionDefinition.ModuleMaxLength);
        RuleFor(query => query.AllowedScopes)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage("Allowed scope is invalid.");
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
