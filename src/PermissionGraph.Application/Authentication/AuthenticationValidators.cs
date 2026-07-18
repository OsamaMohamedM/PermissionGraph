using FluentValidation;

namespace PermissionGraph.Application.Authentication;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(12).MaximumLength(200);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(500);
    }
}

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Token).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Token).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(12).MaximumLength(200);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password);
    }
}

public sealed class UpdateCurrentUserCommandValidator : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
    }
}
