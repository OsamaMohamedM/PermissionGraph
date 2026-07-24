namespace PermissionGraph.Application.Features.RoleAssignments.ExpireRoleAssignments.Validators;

public sealed class ExpireRoleAssignmentsCommandValidator : AbstractValidator<ExpireRoleAssignmentsCommand>
{
    public ExpireRoleAssignmentsCommandValidator()
    {
        RuleFor(command => command.NowUtc).NotEqual(default(DateTimeOffset));
        RuleFor(command => command.BatchSize).InclusiveBetween(1, 500);
    }
}
