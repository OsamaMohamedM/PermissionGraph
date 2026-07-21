namespace PermissionGraph.Api.Configuration;

public static class ApiValidationExtensions
{
    public static IServiceCollection AddApiValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        return services;
    }
}