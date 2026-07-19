using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Authentication;
using PermissionGraph.Application.Common.Errors;

namespace PermissionGraph.Application.Authentication;

public sealed class ValidatingAuthenticationService(
    IAuthenticationService inner,
    IServiceProvider serviceProvider) : IAuthenticationService
{
    public async Task<CurrentUserResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await inner.RegisterAsync(command, cancellationToken);
    }

    public async Task<AuthTokenResult> LoginAsync(LoginCommand command, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await inner.LoginAsync(command, ipAddress, userAgent, cancellationToken);
    }

    public async Task<AuthTokenResult> RefreshAsync(RefreshCommand command, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await inner.RefreshAsync(command, ipAddress, userAgent, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, Guid sessionId, string? ipAddress, CancellationToken cancellationToken)
    {
        await inner.LogoutAsync(userId, sessionId, ipAddress, cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        await inner.LogoutAllAsync(userId, ipAddress, cancellationToken);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        await inner.ConfirmEmailAsync(command, cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        await inner.ForgotPasswordAsync(command, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        await inner.ResetPasswordAsync(command, cancellationToken);
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await inner.GetCurrentUserAsync(userId, cancellationToken);
    }

    public async Task<CurrentUserResult> UpdateCurrentUserAsync(Guid userId, UpdateCurrentUserCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await inner.UpdateCurrentUserAsync(userId, command, cancellationToken);
    }

    private async Task ValidateAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
    {
        var validator = serviceProvider.GetRequiredService<IValidator<TCommand>>();
        var result = await validator.ValidateAsync(command, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        throw new CommandValidationException(errors);
    }
}
