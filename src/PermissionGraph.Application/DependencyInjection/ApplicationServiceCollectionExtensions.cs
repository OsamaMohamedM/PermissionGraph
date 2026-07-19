using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PermissionGraph.Application.Abstractions.Users;
using PermissionGraph.Application.Features.Memberships;
using PermissionGraph.Application.Features.Organizations;

namespace PermissionGraph.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionGraphApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();
        services.AddScoped<AuthenticatedUserResolver>();
        services.AddScoped<OrganizationAccess>();

        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<ListOrganizationsHandler>();
        services.AddScoped<UpdateOrganizationHandler>();
        services.AddScoped<ArchiveOrganizationHandler>();
        services.AddScoped<TransferOwnershipHandler>();

        services.AddScoped<AddOrganizationMemberHandler>();
        services.AddScoped<GetOrganizationMemberHandler>();
        services.AddScoped<ListOrganizationMembersHandler>();
        services.AddScoped<SuspendOrganizationMemberHandler>();
        services.AddScoped<ReactivateOrganizationMemberHandler>();
        services.AddScoped<RemoveOrganizationMemberHandler>();
        services.AddScoped<LeaveOrganizationHandler>();

        return services;
    }
}
