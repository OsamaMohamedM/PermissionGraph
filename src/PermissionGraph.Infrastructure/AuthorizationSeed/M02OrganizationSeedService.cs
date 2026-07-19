using Microsoft.EntityFrameworkCore;
using PermissionGraph.Application.Abstractions.Organizations;
using PermissionGraph.Domain.Organizations;
using PermissionGraph.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace PermissionGraph.Infrastructure.AuthorizationSeed;

internal sealed class M02OrganizationSeedService(PermissionGraphDbContext dbContext) : IOrganizationSeedService
{
    private static readonly PlatformPermissionSeed[] PlatformPermissions =
    [
        new("pg.organizations.view", "Organizations", "View organizations", "Organization"),
        new("pg.organizations.update", "Organizations", "Update organizations", "Organization"),
        new("pg.members.view", "Members", "View members", "Organization"),
        new("pg.members.manage", "Members", "Manage members", "Organization"),
        new("pg.members.suspend", "Members", "Suspend members", "Organization"),
        new("pg.members.remove", "Members", "Remove members", "Organization")
    ];

    private static readonly RoleSeed[] Roles =
    [
        new(
            "Organization Administrator",
            "System organization administrator role.",
            "Organization",
            [
                "pg.organizations.view",
                "pg.organizations.update",
                "pg.members.view",
                "pg.members.manage",
                "pg.members.suspend",
                "pg.members.remove"
            ]),
        new(
            "Organization Member",
            "System organization member role.",
            "Organization",
            [
                "pg.organizations.view",
                "pg.members.view"
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
            var role = new RoleRecord
            {
                Id = DeterministicGuid($"role:{organization.Id}:{roleSeed.Name}"),
                OrganizationId = organization.Id,
                Name = roleSeed.Name,
                NormalizedName = Normalize(roleSeed.Name),
                Description = roleSeed.Description,
                ScopeType = roleSeed.ScopeType,
                RoleType = "System",
                IsRequestable = false,
                IsActive = true,
                CreatedAtUtc = now
            };

            var roleExists = await dbContext.Roles.AnyAsync(
                item => item.OrganizationId == organization.Id && item.NormalizedName == role.NormalizedName && item.ScopeType == role.ScopeType,
                cancellationToken);

            if (!roleExists)
            {
                dbContext.Roles.Add(role);
            }

            foreach (var permissionKey in roleSeed.PermissionKeys)
            {
                var permission = permissionsByKey[Normalize(permissionKey)];
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

    private async Task<Dictionary<string, PermissionDefinitionRecord>> EnsurePlatformPermissionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedKeys = PlatformPermissions.Select(permission => Normalize(permission.Key)).ToArray();
        var existing = await dbContext.PermissionDefinitions
            .Where(permission => permission.OrganizationId == null && normalizedKeys.Contains(permission.NormalizedKey))
            .ToDictionaryAsync(permission => permission.NormalizedKey, cancellationToken);

        foreach (var seed in PlatformPermissions)
        {
            var normalizedKey = Normalize(seed.Key);
            if (existing.ContainsKey(normalizedKey))
            {
                continue;
            }

            var permission = new PermissionDefinitionRecord
            {
                Id = DeterministicGuid($"permission:{normalizedKey}"),
                OrganizationId = null,
                Key = seed.Key,
                NormalizedKey = normalizedKey,
                DisplayName = seed.DisplayName,
                Description = seed.DisplayName,
                Module = seed.Module,
                PermissionType = "Platform",
                AllowedScopes = seed.AllowedScopes,
                IsRequestable = false,
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.PermissionDefinitions.Add(permission);
            existing[normalizedKey] = permission;
        }

        return existing;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private sealed record PlatformPermissionSeed(string Key, string Module, string DisplayName, string AllowedScopes);

    private sealed record RoleSeed(string Name, string Description, string ScopeType, string[] PermissionKeys);
}
