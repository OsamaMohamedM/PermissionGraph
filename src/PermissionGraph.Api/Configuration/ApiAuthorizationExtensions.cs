using Microsoft.AspNetCore.Authorization;
using PermissionGraph.Api.Identity;
using PermissionGraph.Application.Abstractions.Users;

namespace PermissionGraph.Api.Configuration;

public static class ApiAuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
