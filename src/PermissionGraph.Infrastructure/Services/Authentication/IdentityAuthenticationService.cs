namespace PermissionGraph.Infrastructure.Services.Authentication;

internal sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    PermissionGraphDbContext dbContext,
    JwtTokenIssuer jwtTokenIssuer,
    RefreshTokenHasher refreshTokenHasher,
    AuthenticationOptions options,
    IClock clock,
    IEmailDelivery emailDelivery,
    ILogger<IdentityAuthenticationService> logger) : IAuthenticationService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";

    private static UnauthorizedApplicationException InvalidCredentials()
    {
        return new UnauthorizedApplicationException("invalid_credentials", InvalidCredentialsMessage);
    }

    private static UnauthorizedApplicationException InvalidRefreshToken()
    {
        return new UnauthorizedApplicationException("invalid_refresh_token", InvalidRefreshTokenMessage);
    }

    public async Task<CurrentUserResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = command.Email,
            Email = command.Email,
            DisplayName = command.DisplayName,
            CreatedAtUtc = now,
            IsActive = options.NewUsersAreActive,
            EmailConfirmed = options.AutoConfirmEmail
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new ConflictApplicationException("registration_conflict", "Registration could not be completed.");
        }

        if (!options.AutoConfirmEmail)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emailDelivery.SendEmailConfirmationAsync(user.Id, user.Email!, token, cancellationToken);
        }

        return ToCurrentUser(user);
    }

    public async Task<AuthTokenResult> LoginAsync(
        LoginCommand command,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            logger.LogInformation("Login failed for unknown account from {IpAddress}", ipAddress);
            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            logger.LogInformation("Login denied for inactive user {UserId} from {IpAddress}", user.Id, ipAddress);
            throw InvalidCredentials();
        }

        if (options.RequireConfirmedEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            logger.LogInformation("Login denied for unconfirmed user {UserId} from {IpAddress}", user.Id, ipAddress);
            throw InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogInformation("Login denied for locked out user {UserId} from {IpAddress}", user.Id, ipAddress);
            throw InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogInformation("Login failed for user {UserId} from {IpAddress}", user.Id, ipAddress);
            throw InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return await IssueTokenSetAsync(user, null, ipAddress, userAgent, cancellationToken);
    }

    public async Task<AuthTokenResult> RefreshAsync(
        RefreshCommand command,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = refreshTokenHasher.Hash(command.RefreshToken);
        var session = await dbContext.RefreshSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            throw InvalidRefreshToken();
        }

        if (session.RotatedAtUtc is not null || session.ReplacedBySessionId is not null)
        {
            await RevokeFamilyAsync(session.TokenFamilyId, ipAddress, cancellationToken);
            logger.LogWarning("Refresh token reuse detected for user {UserId} and family {TokenFamilyId}", session.UserId, session.TokenFamilyId);
            throw InvalidRefreshToken();
        }

        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now || session.User is null || !session.User.IsActive)
        {
            throw InvalidRefreshToken();
        }

        if (options.RequireConfirmedEmail && !session.User.EmailConfirmed)
        {
            throw InvalidRefreshToken();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await IssueTokenSetAsync(session.User, session.TokenFamilyId, ipAddress, userAgent, cancellationToken);
        var replacement = await dbContext.RefreshSessions.SingleAsync(
            item => item.TokenHash == refreshTokenHasher.Hash(result.RefreshToken),
            cancellationToken);

        session.RotatedAtUtc = now;
        session.ReplacedBySessionId = replacement.Id;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    public async Task LogoutAsync(Guid userId, Guid sessionId, string? ipAddress, CancellationToken cancellationToken)
    {
        var session = await dbContext.RefreshSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);

        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        session.RevokedAtUtc = clock.UtcNow;
        session.RevokedByIp = ipAddress;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await userManager.UpdateSecurityStampAsync(user);
        await RevokeUserSessionsAsync(userId, ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.UserId, out var userId))
        {
            throw new BadRequestApplicationException("email_confirmation_failed", "Email confirmation could not be completed.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new BadRequestApplicationException("email_confirmation_failed", "Email confirmation could not be completed.");
        }

        var result = await userManager.ConfirmEmailAsync(user, command.Token);
        if (!result.Succeeded)
        {
            throw new BadRequestApplicationException("email_confirmation_failed", "Email confirmation could not be completed.");
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailDelivery.SendPasswordResetAsync(user.Id, user.Email!, token, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            throw new BadRequestApplicationException("password_reset_failed", "Password reset could not be completed.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ResetPasswordAsync(user, command.Token, command.Password);
        if (!result.Succeeded)
        {
            throw new BadRequestApplicationException("password_reset_failed", "Password reset could not be completed.");
        }

        await userManager.UpdateSecurityStampAsync(user);
        await RevokeUserSessionsAsync(user.Id, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundApplicationException("user_not_found", "User could not be found.");
        }

        return ToCurrentUser(user);
    }

    public async Task<CurrentUserResult> UpdateCurrentUserAsync(
        Guid userId,
        UpdateCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundApplicationException("user_not_found", "User could not be found.");
        }

        user.DisplayName = command.DisplayName;
        await userManager.UpdateAsync(user);

        return ToCurrentUser(user);
    }

    private async Task<AuthTokenResult> IssueTokenSetAsync(
        ApplicationUser user,
        Guid? familyId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokenGeneratorHelper.Create();
        var now = clock.UtcNow;
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHasher.Hash(refreshToken),
            TokenFamilyId = familyId ?? Guid.NewGuid(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.RefreshTokenDays),
            CreatedByIp = ipAddress,
            UserAgentHash = refreshTokenHasher.HashUserAgent(userAgent)
        };

        dbContext.RefreshSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenIssuer.Issue(user, session.Id);

        return new AuthTokenResult(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken,
            session.ExpiresAtUtc);
    }

    private async Task RevokeFamilyAsync(Guid tokenFamilyId, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await dbContext.RefreshSessions
            .Where(item => item.TokenFamilyId == tokenFamilyId && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.RevokedAtUtc, now)
                    .SetProperty(item => item.RevokedByIp, ipAddress),
                cancellationToken);
    }

    private async Task RevokeUserSessionsAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await dbContext.RefreshSessions
            .Where(item => item.UserId == userId && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.RevokedAtUtc, now)
                    .SetProperty(item => item.RevokedByIp, ipAddress),
                cancellationToken);
    }

    private static CurrentUserResult ToCurrentUser(ApplicationUser user)
    {
        return new CurrentUserResult(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.EmailConfirmed,
            user.IsActive);
    }
}