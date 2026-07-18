using System.Security.Claims;
using PermissionGraph.Api.Validation;
using PermissionGraph.Application.Abstractions.Authentication;
using PermissionGraph.Application.Authentication;
using PermissionGraph.Contracts.Authentication;

namespace PermissionGraph.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth");

        auth.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-register")
            .AddEndpointFilter<ValidationFilter<RegisterCommand>>();

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-login")
            .AddEndpointFilter<ValidationFilter<LoginCommand>>();

        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .AddEndpointFilter<ValidationFilter<RefreshCommand>>();

        auth.MapPost("/logout", LogoutAsync);

        auth.MapPost("/logout-all", LogoutAllAsync);

        auth.MapPost("/confirm-email", ConfirmEmailAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-confirm-email")
            .AddEndpointFilter<ValidationFilter<ConfirmEmailCommand>>();

        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-forgot-password")
            .AddEndpointFilter<ValidationFilter<ForgotPasswordCommand>>();

        auth.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-reset-password")
            .AddEndpointFilter<ValidationFilter<ResetPasswordCommand>>();

        var users = app.MapGroup("/api/v1/users");

        users.MapGet("/me", GetMeAsync);

        users.MapPatch("/me", UpdateMeAsync)
            .AddEndpointFilter<ValidationFilter<UpdateCurrentUserCommand>>();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authenticationService.RegisterAsync(command, cancellationToken);
            return Results.Created($"/api/v1/users/{result.UserId}", ToCurrentUserResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Problem(context, StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authenticationService.LoginAsync(command, GetIpAddress(context), GetUserAgent(context), cancellationToken);
            return Results.Ok(ToAuthResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Problem(context, StatusCodes.Status401Unauthorized, exception.Message);
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authenticationService.RefreshAsync(command, GetIpAddress(context), GetUserAgent(context), cancellationToken);
            return Results.Ok(ToAuthResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Problem(context, StatusCodes.Status401Unauthorized, exception.Message);
        }
    }

    private static async Task<IResult> LogoutAsync(
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var identity = GetAuthenticatedSession(context);
        if (identity is null)
        {
            return Results.Unauthorized();
        }

        await authenticationService.LogoutAsync(identity.Value.UserId, identity.Value.SessionId, GetIpAddress(context), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId(context);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        await authenticationService.LogoutAllAsync(userId.Value, GetIpAddress(context), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await authenticationService.ConfirmEmailAsync(command, cancellationToken);
            return Results.NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Problem(context, StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordCommand command,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        await authenticationService.ForgotPasswordAsync(command, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await authenticationService.ResetPasswordAsync(command, cancellationToken);
            return Results.NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Problem(context, StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> GetMeAsync(
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId(context);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await authenticationService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return Results.Ok(ToCurrentUserResponse(result));
    }

    private static async Task<IResult> UpdateMeAsync(
        UpdateCurrentUserCommand command,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId(context);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await authenticationService.UpdateCurrentUserAsync(userId.Value, command, cancellationToken);
        return Results.Ok(ToCurrentUserResponse(result));
    }

    private static AuthResponse ToAuthResponse(AuthTokenResult result)
    {
        return new AuthResponse(
            result.UserId,
            result.Email,
            result.DisplayName,
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc);
    }

    private static CurrentUserResponse ToCurrentUserResponse(CurrentUserResult result)
    {
        return new CurrentUserResponse(
            result.UserId,
            result.Email,
            result.DisplayName,
            result.EmailConfirmed,
            result.IsActive);
    }

    private static (Guid UserId, Guid SessionId)? GetAuthenticatedSession(HttpContext context)
    {
        var userId = GetAuthenticatedUserId(context);
        var sessionIdValue = context.User.FindFirstValue("session_id");

        if (userId is null || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            return null;
        }

        return (userId.Value, sessionId);
    }

    private static Guid? GetAuthenticatedUserId(HttpContext context)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static string? GetIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? GetUserAgent(HttpContext context)
    {
        return context.Request.Headers.UserAgent.ToString();
    }

    private static IResult Problem(HttpContext context, int statusCode, string title)
    {
        return Results.Problem(
            title: title,
            statusCode: statusCode,
            instance: context.Request.Path);
    }
}
