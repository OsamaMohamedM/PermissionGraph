namespace PermissionGraph.Api.Validation;

public sealed class AuthorizationCheckRequestValidator : AbstractValidator<AuthorizationCheckRequest>
{
    public AuthorizationCheckRequestValidator()
    {
        RuleFor(request => request.SubjectUserId)
            .Must(subjectUserId => subjectUserId is null || subjectUserId.Value != Guid.Empty)
            .WithMessage("Subject user identifier cannot be empty when provided.");

        RuleFor(request => request.ProjectId)
            .Must(projectId => projectId is null || projectId.Value != Guid.Empty)
            .WithMessage("Project identifier cannot be empty when provided.");

        RuleFor(request => request.PermissionKey)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.");
    }
}

public sealed class AuthorizationBatchCheckRequestValidator : AbstractValidator<AuthorizationBatchCheckRequest>
{
    public AuthorizationBatchCheckRequestValidator()
    {
        RuleFor(request => request.Checks)
            .NotEmpty()
            .Must(checks => checks.Count <= BatchCheckPermissionsQuery.MaxChecks)
            .WithMessage($"Batch authorization checks cannot contain more than {BatchCheckPermissionsQuery.MaxChecks} items.");

        RuleForEach(request => request.Checks)
            .SetValidator(new AuthorizationBatchCheckItemRequestValidator());
    }
}

public sealed class ExplainAccessRequestValidator : AbstractValidator<ExplainAccessRequest>
{
    public ExplainAccessRequestValidator()
    {
        RuleFor(request => request.SubjectUserId)
            .Must(subjectUserId => subjectUserId is null || subjectUserId.Value != Guid.Empty)
            .WithMessage("Subject user identifier cannot be empty when provided.");

        RuleFor(request => request.ProjectId)
            .Must(projectId => projectId is null || projectId.Value != Guid.Empty)
            .WithMessage("Project identifier cannot be empty when provided.");

        RuleFor(request => request.PermissionKey)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.");

        RuleFor(request => request.EvaluatedAtUtc)
            .Null()
            .WithMessage("Historical access explanation is not supported.");
    }
}

internal sealed class AuthorizationBatchCheckItemRequestValidator : AbstractValidator<AuthorizationBatchCheckItemRequest>
{
    public AuthorizationBatchCheckItemRequestValidator()
    {
        RuleFor(request => request.CorrelationId)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(request => request.SubjectUserId)
            .Must(subjectUserId => subjectUserId is null || subjectUserId.Value != Guid.Empty)
            .WithMessage("Subject user identifier cannot be empty when provided.");

        RuleFor(request => request.ProjectId)
            .Must(projectId => projectId is null || projectId.Value != Guid.Empty)
            .WithMessage("Project identifier cannot be empty when provided.");

        RuleFor(request => request.PermissionKey)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.");
    }
}
