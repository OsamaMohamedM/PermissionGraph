namespace PermissionGraph.Application.Features.Authorization.BatchCheckPermissions.Validators;

public sealed class BatchCheckPermissionsQueryValidator : AbstractValidator<BatchCheckPermissionsQuery>
{
    public BatchCheckPermissionsQueryValidator()
    {
        RuleFor(query => query.Checks)
            .NotEmpty()
            .Must(checks => checks.Count <= BatchCheckPermissionsQuery.MaxChecks)
            .WithMessage($"Batch authorization checks cannot contain more than {BatchCheckPermissionsQuery.MaxChecks} items.");

        RuleForEach(query => query.Checks)
            .SetValidator(new BatchCheckPermissionItemValidator());
    }
}

internal sealed class BatchCheckPermissionItemValidator : AbstractValidator<BatchCheckPermissionItem>
{
    public BatchCheckPermissionItemValidator()
    {
        RuleFor(item => item.CorrelationId)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(item => item.ToCheckPermissionQuery())
            .SetValidator(new CheckPermissionQueryValidator());
    }
}
