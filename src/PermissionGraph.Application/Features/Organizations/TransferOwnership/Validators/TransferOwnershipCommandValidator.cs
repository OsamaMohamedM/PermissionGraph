namespace PermissionGraph.Application.Features.Organizations.TransferOwnership.Validators;

public sealed class TransferOwnershipCommandValidator : AbstractValidator<TransferOwnershipCommand>
{
    public TransferOwnershipCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.NewOwnerUserId).NotEmpty();
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(200);
    }
}