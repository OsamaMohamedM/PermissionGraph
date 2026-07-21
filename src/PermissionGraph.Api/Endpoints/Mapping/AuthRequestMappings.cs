namespace PermissionGraph.Api.Endpoints.Mapping;

internal static class AuthRequestMappings
{
    public static RegisterCommand ToCommand(this RegisterRequest request)
    {
        return new RegisterCommand(request.DisplayName, request.Email, request.Password, request.ConfirmPassword);
    }

    public static LoginCommand ToCommand(this LoginRequest request)
    {
        return new LoginCommand(request.Email, request.Password);
    }

    public static RefreshCommand ToCommand(this RefreshRequest request)
    {
        return new RefreshCommand(request.RefreshToken);
    }

    public static ConfirmEmailCommand ToCommand(this ConfirmEmailRequest request)
    {
        return new ConfirmEmailCommand(request.UserId, request.Token);
    }

    public static ForgotPasswordCommand ToCommand(this ForgotPasswordRequest request)
    {
        return new ForgotPasswordCommand(request.Email);
    }

    public static ResetPasswordCommand ToCommand(this ResetPasswordRequest request)
    {
        return new ResetPasswordCommand(request.Email, request.Token, request.Password, request.ConfirmPassword);
    }

    public static UpdateCurrentUserCommand ToCommand(this UpdateCurrentUserRequest request)
    {
        return new UpdateCurrentUserCommand(request.DisplayName);
    }
}