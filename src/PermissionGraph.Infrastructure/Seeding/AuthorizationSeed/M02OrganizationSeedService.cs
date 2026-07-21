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
                "pg.organizations.archive",
                "pg.organizations.transfer_ownership",
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
            ])
    ];

    public async Task SeedDefaultAuthorizationAsync(
        Organization organization,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = organization.CreatedAtUtc;
        var permissionsByKey = await EnsurePlatformPermissionsAsync(now, cancellationToken);

        foreach (var roleSeed in Roles)
        {
            var normalizedRoleName = NormalizeRoleName(roleSeed.Name);
            var role = await dbContext.Roles.SingleOrDefaultAsync(
                item => item.OrganizationId == organization.Id && item.NormalizedName == normalizedRoleName && item.ScopeType == roleSeed.ScopeType,
                cancellationToken);

            if (role is null)
            {
                role = new RoleRecord
                {
                    Id = DeterministicGuid($"role:{organization.Id}:{roleSeed.Name}"),
                    OrganizationId = organization.Id,
                    Name = roleSeed.Name,
                    NormalizedName = normalizedRoleName,
                    Description = roleSeed.Description,
                    ScopeType = roleSeed.ScopeType,
                    RoleType = "System",
                    IsRequestable = false,
                    IsActive = true,
                    CreatedAtUtc = now
                };

                dbContext.Roles.Add(role);
            }

            foreach (var permissionKey in roleSeed.PermissionKeys)
            {
                var permission = permissionsByKey[NormalizePermissionKey(permissionKey)];
                var mappingExists = await dbContext.RolePermissions.AnyAsync(
                    item => item.RoleId == role.Id && item.PermissionId == permission.Id,
                    cancellationToken);

                if (!mappingExists)
                {
                    dbContext.RolePermissions.Add(new RolePermissionRecord
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        AddedAtUtc = now,
                        AddedByUserId = actorUserId
                    });
                }
            }
        }
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
            if (existing.ContainsKey(normalizedKey))
            {
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