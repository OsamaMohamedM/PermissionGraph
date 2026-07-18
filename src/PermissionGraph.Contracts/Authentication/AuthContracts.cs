namespace PermissionGraph.Contracts.Authentication;

public sealed record RegisterRequest(string DisplayName, string Email, string Password, string ConfirmPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ConfirmEmailRequest(string UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string Password, string ConfirmPassword);

public sealed record UpdateCurrentUserRequest(string DisplayName);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record CurrentUserResponse(Guid UserId, string Email, string DisplayName, bool EmailConfirmed, bool IsActive);
