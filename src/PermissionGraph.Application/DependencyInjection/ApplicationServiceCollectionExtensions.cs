namespace PermissionGraph.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionGraphApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();
        services.AddScoped<AuthenticatedUserResolver>();
        services.AddScoped<OrganizationAccessHelper>();
        services.AddScoped<ProjectAccessHelper>();
        services.AddScoped<PermissionCatalogAccessHelper>();
        services.AddScoped<RoleCatalogAccessHelper>();
        services.AddScoped<IAuthorizationDecisionCache, NoOpAuthorizationDecisionCache>();
        services.AddScoped<IAuthorizationDecisionService, AuthorizationDecisionService>();
        services.AddScoped<ExplainAccessHandler>();

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

        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ListProjectsHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();

        services.AddScoped<ListPermissionsHandler>();
        services.AddScoped<GetPermissionHandler>();
        services.AddScoped<CreateCustomPermissionHandler>();
        services.AddScoped<UpdateCustomPermissionHandler>();
        services.AddScoped<ArchiveCustomPermissionHandler>();
        services.AddScoped<ActivateCustomPermissionHandler>();

        services.AddScoped<ListRolesHandler>();
        services.AddScoped<GetRoleHandler>();
        services.AddScoped<CreateCustomRoleHandler>();
        services.AddScoped<UpdateCustomRoleHandler>();
        services.AddScoped<CloneRoleHandler>();
        services.AddScoped<ArchiveCustomRoleHandler>();
        services.AddScoped<ActivateCustomRoleHandler>();
        services.AddScoped<ReplaceRolePermissionsHandler>();

        services.AddScoped<AssignRoleHandler>();
        services.AddScoped<GetRoleAssignmentHandler>();
        services.AddScoped<ListRoleAssignmentsHandler>();
        services.AddScoped<RevokeRoleAssignmentHandler>();
        services.AddScoped<ExpireRoleAssignmentsHandler>();

        return services;
    }
}
