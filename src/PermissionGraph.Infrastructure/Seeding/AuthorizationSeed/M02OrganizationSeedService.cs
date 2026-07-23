namespace PermissionGraph.Infrastructure.Seeding.AuthorizationSeed;

internal sealed class M02OrganizationSeedService(PermissionGraphDbContext dbContext) : IOrganizationSeedService
{
    private static readonly RoleSeed[] Roles =
    [
        new(
            "Organization Administrator",
            "System organization administrator role.",
            "Organization",
            [
                "pg.organizations.view",
                "pg.organizations.create",
                "pg.organizations.update",
                "pg.members.view",
                "pg.members.manage",
                "pg.members.suspend",
                "pg.members.remove",
                "pg.projects.create",
                "pg.projects.view",
                "pg.projects.update",
                "pg.projects.archive",
                "pg.roles.view",
                "pg.roles.create",
                "pg.roles.update",
                "pg.roles.archive",
                "pg.roles.assign",
                "pg.permissions.view",
                "pg.permissions.create",
                "pg.permissions.update",
                "pg.permissions.archive",
                "pg.access_requests.view_all",
                "pg.access_requests.review",
                "pg.authorization.check",
                "pg.authorization.check_other_users",
                "pg.authorization.explain_self",
                "pg.authorization.explain_others",
                "pg.audit.view"
            ]),
        new(
            "Organization Member",
            "System organization member role.",
            "Organization",
            [
                "pg.organizations.view",
                "pg.members.view",
                "pg.projects.view",
                "pg.access_requests.create",
                "pg.access_requests.view_own",
                "pg.authorization.check",
                "pg.authorization.explain_self"
            ]),
        new(
            "Project Administrator",
            "System project administrator role.",
            "Project",
            [
                "pg.projects.view",
                "pg.projects.update",
                "pg.projects.archive",
                "pg.roles.view",
                "pg.roles.assign",
                "pg.permissions.view",
                "pg.access_requests.view_all",
                "pg.access_requests.review",
                "pg.authorization.check",
                "pg.authorization.check_other_users",
                "pg.authorization.explain_self",
                "pg.authorization.explain_others"
            ]),
        new(
            "Project Contributor",
            "System project contributor role.",
            "Project",
            [
                "pg.projects.view",
                "pg.access_requests.create",
                "pg.access_requests.view_own",
                "pg.authorization.check",
                "pg.authorization.explain_self"
            ]),
        new(
            "Project Viewer",
            "System project viewer role.",
            "Project",
            [
                "pg.projects.view",
                "pg.access_requests.create",
                "pg.access_requests.view_own",
                "pg.authorization.check",
                "pg.authorization.explain_self"
            ])
    ];

    private static readonly string[] ForbiddenOrganizationAdministratorPermissionKeys =
    [
        "pg.organizations.archive",
        "pg.organizations.transfer_ownership"
    ];

    public async Task SeedDefaultAuthorizationAsync(
        Organization organization,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = organization.CreatedAtUtc;
        var permissionsByKey = await EnsurePlatformPermissionsAsync(now, cancellationToken);

        await RemoveForbiddenOrganizationAdministratorMappingsAsync(organization.Id, cancellationToken);

        foreach (var roleSeed in Roles)
        {
            var normalizedRoleName = NormalizeRoleName(roleSeed.Name);
            var roleScopeType = Enum.Parse<RoleScopeType>(roleSeed.ScopeType);
            var role = await dbContext.Roles.SingleOrDefaultAsync(
                item => item.OrganizationId == organization.Id && item.NormalizedName == normalizedRoleName && item.ScopeType == roleScopeType,
                cancellationToken);
            var roleAlreadyExisted = role is not null;

            if (role is null)
            {
                var rolePermissions = roleSeed.PermissionKeys
                    .Select(permissionKey => permissionsByKey[NormalizePermissionKey(permissionKey)])
                    .ToArray();
                role = Role.CreateSystem(
                    DeterministicGuid($"role:{organization.Id}:{roleSeed.Name}"),
                    organization.Id,
                    roleSeed.Name,
                    normalizedRoleName,
                    roleSeed.Description,
                    roleScopeType,
                    isRequestable: false,
                    now,
                    rolePermissions,
                    actorUserId);

                dbContext.Roles.Add(role);
            }

            foreach (var permissionKey in roleSeed.PermissionKeys)
            {
                if (!roleAlreadyExisted)
                {
                    continue;
                }

                var permission = permissionsByKey[NormalizePermissionKey(permissionKey)];
                var mappingExists = await dbContext.RolePermissions.AnyAsync(
                    item => item.RoleId == role.Id && item.PermissionId == permission.Id,
                    cancellationToken);

                if (!mappingExists)
                {
                    await InsertRolePermissionIfMissingAsync(role.Id, permission.Id, now, actorUserId, cancellationToken);
                }
            }
        }
    }

    private async Task RemoveForbiddenOrganizationAdministratorMappingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var normalizedKeys = ForbiddenOrganizationAdministratorPermissionKeys
            .Select(NormalizePermissionKey)
            .ToArray();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "RolePermissions" role_permission
             USING "Roles" role, "PermissionDefinitions" permission
             WHERE role_permission."RoleId" = role."Id"
               AND role_permission."PermissionId" = permission."Id"
               AND role."OrganizationId" = {organizationId}
               AND role."NormalizedName" = 'ORGANIZATION ADMINISTRATOR'
               AND role."ScopeType" = 'Organization'
               AND permission."OrganizationId" IS NULL
               AND permission."NormalizedKey" = ANY({normalizedKeys})
             """,
            cancellationToken);
    }

    private async Task InsertRolePermissionIfMissingAsync(
        Guid roleId,
        Guid permissionId,
        DateTimeOffset addedAtUtc,
        Guid addedByUserId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "AddedAtUtc", "AddedByUserId")
             SELECT {roleId}, {permissionId}, {addedAtUtc}, {addedByUserId}
             WHERE NOT EXISTS (
                 SELECT 1
                 FROM "RolePermissions"
                 WHERE "RoleId" = {roleId}
                   AND "PermissionId" = {permissionId})
             """,
            cancellationToken);
    }

    private async Task<Dictionary<string, PermissionDefinition>> EnsurePlatformPermissionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedKeys = PlatformPermissionCatalog.All.Select(permission => permission.NormalizedKey).ToArray();
        var existing = await dbContext.PermissionDefinitions
            .Where(permission => permission.OrganizationId == null && normalizedKeys.Contains(permission.NormalizedKey))
            .ToDictionaryAsync(permission => permission.NormalizedKey, cancellationToken);

        foreach (var seed in PlatformPermissionCatalog.All)
        {
            var normalizedKey = seed.NormalizedKey;
            if (existing.TryGetValue(normalizedKey, out var existingPermission))
            {
                if (existingPermission.AllowedScopes != seed.AllowedScopes)
                {
                    dbContext.Entry(existingPermission)
                        .Property(permission => permission.AllowedScopes)
                        .CurrentValue = seed.AllowedScopes;

                    dbContext.Entry(existingPermission)
                        .Property(permission => permission.UpdatedAtUtc)
                        .CurrentValue = now;
                }

                continue;
            }

            var permission = PlatformPermissionCatalog.ToPermissionDefinition(seed, now);

            dbContext.PermissionDefinitions.Add(permission);
            existing[normalizedKey] = permission;
        }

        return existing;
    }

    private static string NormalizeRoleName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizePermissionKey(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private sealed record RoleSeed(string Name, string Description, string ScopeType, string[] PermissionKeys);
}
