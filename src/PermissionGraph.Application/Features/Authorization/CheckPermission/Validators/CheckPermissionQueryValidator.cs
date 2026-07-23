namespace PermissionGraph.Application.Features.Authorization.CheckPermission.Validators;

public sealed class CheckPermissionQueryValidator : AbstractValidator<CheckPermissionQuery>
{
    public CheckPermissionQueryValidator()
    {
        RuleFor(query => query.SubjectUserId)
            .Must(subjectUserId => subjectUserId is null || subjectUserId.Value != Guid.Empty)
            .WithMessage("Subject user identifier cannot be empty when provided.");

        RuleFor(query => query.OrganizationId).NotEmpty();

        RuleFor(query => query.ProjectId)
            .Must(projectId => projectId is null || projectId.Value != Guid.Empty)
            .WithMessage("Project identifier cannot be empty when provided.");

        RuleFor(query => query.PermissionKey)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.");

        RuleFor(query => query.RequestedEvaluationTimeUtc)
            .Null()
            .WithMessage("Historical authorization checks are not supported by normal permission checks.");
    }
}
