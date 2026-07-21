namespace PermissionGraph.Api.Configuration;

public static class ApiAuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authenticationOptions = AuthenticationOptions.FromConfiguration(configuration);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authenticationOptions.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = authenticationOptions.JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authenticationOptions.JwtSigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirstValue("sub");
                        var securityStamp = context.Principal?.FindFirstValue(TokenClaimsHelper.SecurityStamp);

                        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
                        {
                            context.Fail("Invalid token claims.");
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PermissionGraphDbContext>();
                        var user = await dbContext.Users.FindAsync([userId], context.HttpContext.RequestAborted);
                        if (user is null || !user.IsActive || user.SecurityStamp != securityStamp)
                        {
                            context.Fail("Token security stamp is no longer valid.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Authentication is required.");
                    },
                    OnForbidden = async context =>
                    {
                        await WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Access is forbidden.");
                    }
                };
            });

        return services;
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            context.RequestAborted);
    }
}