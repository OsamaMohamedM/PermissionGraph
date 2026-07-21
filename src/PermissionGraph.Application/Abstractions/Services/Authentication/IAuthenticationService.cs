namespace PermissionGraph.Application.Abstractions.Services.Authentication;

public interface IAuthenticationService
{
    Task<CurrentUserResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);

    Task<AuthTokenResult> LoginAsync(LoginCommand command, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task<AuthTokenResult> RefreshAsync(RefreshCommand command, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task LogoutAsync(Guid userId, Guid sessionId, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken);

    Task ConfirmEmailAsync(ConfirmEmailCommand command, CancellationToken cancellationToken);

    Task ForgotPasswordAsync(ForgotPasswordCommand command, CancellationToken cancellationToken);

    Task ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken);

    Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<CurrentUserResult> UpdateCurrentUserAsync(Guid userId, UpdateCurrentUserCommand command, CancellationToken cancellationToken);
}