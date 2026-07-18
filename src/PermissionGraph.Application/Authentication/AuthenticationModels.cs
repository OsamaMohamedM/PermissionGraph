namespace PermissionGraph.Application.Authentication;

public sealed record RegisterCommand(string DisplayName, string Email, string Password, string ConfirmPassword);

public sealed record LoginCommand(string Email, string Password);

public sealed record RefreshCommand(string RefreshToken);

public sealed record ConfirmEmailCommand(string UserId, string Token);

public sealed record ForgotPasswordCommand(string Email);

public sealed record ResetPasswordCommand(string Email, string Token, string Password, string ConfirmPassword);

public sealed record UpdateCurrentUserCommand(string DisplayName);

public sealed record AuthTokenResult(
    Guid UserId,
    string Email,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record CurrentUserResult(Guid UserId, string Email, string DisplayName, bool EmailConfirmed, bool IsActive);
