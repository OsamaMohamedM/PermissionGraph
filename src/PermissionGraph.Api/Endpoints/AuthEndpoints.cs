namespace PermissionGraph.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth");

        auth.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-register")
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-login")
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-refresh")
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>();

        auth.MapPost("/logout", LogoutAsync);

        auth.MapPost("/logout-all", LogoutAllAsync);

        auth.MapPost("/confirm-email", ConfirmEmailAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-confirm-email")
            .AddEndpointFilter<ValidationFilter<ConfirmEmailRequest>>();

        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-forgot-password")
            .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>();

        auth.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-reset-password")
            .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>();

        var users = app.MapGroup("/api/v1/users");

        users.MapGet("/me", GetMeAsync);

        users.MapPatch("/me", UpdateMeAsync)
            .AddEndpointFilter<ValidationFilter<UpdateCurrentUserRequest>>();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(request.ToCommand(), cancellationToken);
        return Results.Json(ToCurrentUserResponse(result), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request.ToCommand(), GetIpAddress(context), GetUserAgent(context), cancellationToken);
        return Results.Ok(ToAuthResponse(result));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(request.ToCommand(), GetIpAddress(context), GetUserAgent(context), cancellationToken);
        return Results.Ok(ToAuthResponse(result));
    }

    private static async Task<IResult> LogoutAsync(
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var identity = GetAuthenticatedSession(context);
        if (identity is null)
        {
            throw new UnauthorizedApplicationException("invalid_token", "Authentication is required.");
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
            throw new UnauthorizedApplicationException("invalid_token", "Authentication is required.");
        }

        await authenticationService.LogoutAllAsync(userId.Value, GetIpAddress(context), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        await authenticationService.ConfirmEmailAsync(request.ToCommand(), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        await authenticationService.ForgotPasswordAsync(request.ToCommand(), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        await authenticationService.ResetPasswordAsync(request.ToCommand(), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId(context);
        if (userId is null)
        {
            throw new UnauthorizedApplicationException("invalid_token", "Authentication is required.");
        }

        var result = await authenticationService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return Results.Ok(ToCurrentUserResponse(result));
    }

    private static async Task<IResult> UpdateMeAsync(
        UpdateCurrentUserRequest request,
        IAuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId(context);
        if (userId is null)
        {
            throw new UnauthorizedApplicationException("invalid_token", "Authentication is required.");
        }

        var result = await authenticationService.UpdateCurrentUserAsync(userId.Value, request.ToCommand(), cancellationToken);
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
}