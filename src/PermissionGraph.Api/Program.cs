using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PermissionGraph.Api.Configuration;
using PermissionGraph.Api.Endpoints;
using PermissionGraph.Application.DependencyInjection;
using PermissionGraph.Infrastructure.Authentication;
using PermissionGraph.Infrastructure.Configuration;
using PermissionGraph.Infrastructure.Data;
using PermissionGraph.Infrastructure.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .CreateLogger();

try
{
    LocalEnvironmentFile.LoadIfPresent();

    var builder = WebApplication.CreateBuilder(args);

    StartupValidation.ValidateFoundationConfiguration(builder.Configuration);

    builder.Host.UseSerilog();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();
    builder.Services.AddPermissionGraphApplication();
    builder.Services.AddPermissionGraphInfrastructure(builder.Configuration);

    var authenticationOptions = AuthenticationOptions.FromConfiguration(builder.Configuration);

    builder.Services
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
                    var securityStamp = context.Principal?.FindFirstValue(TokenClaims.SecurityStamp);

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
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        AddFixedIpPolicy(options, "auth-register", 5, TimeSpan.FromMinutes(10));
        AddFixedIpPolicy(options, "auth-login", 10, TimeSpan.FromMinutes(1));
        AddFixedIpPolicy(options, "auth-refresh", 30, TimeSpan.FromMinutes(1));
        AddFixedIpPolicy(options, "auth-confirm-email", 10, TimeSpan.FromMinutes(10));
        AddFixedIpPolicy(options, "auth-forgot-password", 3, TimeSpan.FromMinutes(15));
        AddFixedIpPolicy(options, "auth-reset-password", 5, TimeSpan.FromMinutes(15));
    });

    var databaseConnection = InfrastructureServiceCollectionExtensions.RequireConnectionString(
        builder.Configuration,
        "PermissionGraph");
    var redisConnection = InfrastructureServiceCollectionExtensions.RequireConnectionString(
        builder.Configuration,
        "Redis");

    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddNpgSql(databaseConnection, name: "postgresql", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
        .AddRedis(redisConnection, name: "redis", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

    var app = builder.Build();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            if (app.Environment.IsDevelopment() && exceptionFeature?.Error is not null)
            {
                problem.Detail = exceptionFeature.Error.Message;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        });
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
    }

    if (app.Environment.IsEnvironment("Testing"))
    {
        app.MapGet("/__test/problem", (HttpContext _) =>
            throw new InvalidOperationException("Test exception details"))
            .AllowAnonymous();
    }

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", HealthCheckResponseWriter.Live).AllowAnonymous();
    app.MapHealthChecks("/health/ready", HealthCheckResponseWriter.Ready).AllowAnonymous();
    app.MapAuthenticationEndpoints();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "PermissionGraph API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static void AddFixedIpPolicy(RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window)
{
    options.AddPolicy(policyName, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
}

public partial class Program;
