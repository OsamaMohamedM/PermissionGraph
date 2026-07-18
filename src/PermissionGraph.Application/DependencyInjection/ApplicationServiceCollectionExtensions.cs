using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PermissionGraph.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionGraphApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();

        return services;
    }
}
