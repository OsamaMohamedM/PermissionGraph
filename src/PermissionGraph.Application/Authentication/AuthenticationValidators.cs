namespace PermissionGraph.Application.Authentication;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(200);
        RuleFor(command => command.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.").MinimumLength(12).WithMessage("Password does not meet the minimum length.").MaximumLength(200);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password).WithMessage("Password confirmation must match.");
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.").MaximumLength(200);
    }
}

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().WithMessage("Refresh token is required.").MaximumLength(500);
    }
}

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage("User id is required.");
        RuleFor(command => command.Token).NotEmpty().WithMessage("Token is required.").MaximumLength(2000);
    }
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(command => command.Token).NotEmpty().WithMessage("Token is required.").MaximumLength(2000);
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.").MinimumLength(12).WithMessage("Password does not meet the minimum length.").MaximumLength(200);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.Password).WithMessage("Password confirmation must match.");
    }
}

public sealed class UpdateCurrentUserCommandValidator : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(200);
    }
}