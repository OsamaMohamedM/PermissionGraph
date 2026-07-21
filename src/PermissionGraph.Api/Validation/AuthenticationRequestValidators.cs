namespace PermissionGraph.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().WithMessage("Password is required.").MaximumLength(200);
        RuleFor(request => request.ConfirmPassword).Equal(request => request.Password).WithMessage("Password confirmation must match.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().WithMessage("Password is required.").MaximumLength(200);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty().WithMessage("Refresh token is required.").MaximumLength(500);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(request => request.UserId)
            .NotEmpty()
            .WithMessage("User id is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("User id must be valid.");

        RuleFor(request => request.Token).NotEmpty().WithMessage("Token is required.").MaximumLength(2000);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().WithMessage("Email is required.").EmailAddress().WithMessage("Email must be valid.").MaximumLength(320);
        RuleFor(request => request.Token).NotEmpty().WithMessage("Token is required.").MaximumLength(2000);
        RuleFor(request => request.Password).NotEmpty().WithMessage("Password is required.").MaximumLength(200);
        RuleFor(request => request.ConfirmPassword).Equal(request => request.Password).WithMessage("Password confirmation must match.");
    }
}

public sealed class UpdateCurrentUserRequestValidator : AbstractValidator<UpdateCurrentUserRequest>
{
    public UpdateCurrentUserRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().WithMessage("Display name is required.").MaximumLength(200);
    }
}