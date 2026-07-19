using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PermissionGraph.Api.Configuration;

public static class ApiRateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken);
            };

            AddFixedIpPolicy(options, "auth-register", 5, TimeSpan.FromMinutes(10));
            AddFixedIpPolicy(options, "auth-login", 10, TimeSpan.FromMinutes(1));
            AddFixedIpPolicy(options, "auth-refresh", 30, TimeSpan.FromMinutes(1));
            AddFixedIpPolicy(options, "auth-confirm-email", 10, TimeSpan.FromMinutes(10));
            AddFixedIpPolicy(options, "auth-forgot-password", 3, TimeSpan.FromMinutes(15));
            AddFixedIpPolicy(options, "auth-reset-password", 5, TimeSpan.FromMinutes(15));
            AddFixedIpPolicy(options, "org-transfer-ownership", 5, TimeSpan.FromMinutes(10));
            AddFixedIpPolicy(options, "org-member-add", 20, TimeSpan.FromMinutes(1));
            AddFixedIpPolicy(options, "org-member-mutations", 30, TimeSpan.FromMinutes(1));
            AddFixedIpPolicy(options, "org-mutations", 30, TimeSpan.FromMinutes(1));
        });

        return services;
    }

    private static void AddFixedIpPolicy(RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window)
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
}
