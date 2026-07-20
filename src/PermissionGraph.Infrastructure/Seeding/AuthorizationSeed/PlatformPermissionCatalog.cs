using System.Security.Cryptography;
using System.Text;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Infrastructure.AuthorizationSeed;

internal static class PlatformPermissionCatalog
{
    public static IReadOnlyList<PlatformPermissionCatalogEntry> All { get; } =
    [
        Entry("pg.organizations.view", "Organizations", "View organizations", "Allows reading visible organization details.", PermissionAllowedScopes.Organization),
        Entry("pg.organizations.create", "Organizations", "Create organizations", "Allows creating organizations.", PermissionAllowedScopes.Organization),
        Entry("pg.organizations.update", "Organizations", "Update organizations", "Allows updating organization details.", PermissionAllowedScopes.Organization),
        Entry("pg.organizations.archive", "Organizations", "Archive organizations", "Allows archiving organizations.", PermissionAllowedScopes.Organization),
        Entry("pg.organizations.transfer_ownership", "Organizations", "Transfer ownership", "Allows transferring organization ownership.", PermissionAllowedScopes.Organization),

        Entry("pg.members.view", "Members", "View members", "Allows reading organization membership details.", PermissionAllowedScopes.Organization),
        Entry("pg.members.manage", "Members", "Manage members", "Allows adding organization members.", PermissionAllowedScopes.Organization),
        Entry("pg.members.suspend", "Members", "Suspend members", "Allows suspending organization members.", PermissionAllowedScopes.Organization),
        Entry("pg.members.remove", "Members", "Remove members", "Allows removing organization members.", PermissionAllowedScopes.Organization),

        Entry("pg.projects.create", "Projects", "Create projects", "Allows creating projects in an organization.", PermissionAllowedScopes.Organization),
        Entry("pg.projects.view", "Projects", "View projects", "Allows reading visible project details.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.projects.update", "Projects", "Update projects", "Allows updating project details.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.projects.archive", "Projects", "Archive projects", "Allows archiving projects.", PermissionAllowedScopes.OrganizationAndProject),

        Entry("pg.roles.view", "Roles", "View roles", "Allows reading role definitions.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.roles.create", "Roles", "Create roles", "Allows creating custom roles.", PermissionAllowedScopes.Organization),
        Entry("pg.roles.update", "Roles", "Update roles", "Allows updating custom roles.", PermissionAllowedScopes.Organization),
        Entry("pg.roles.archive", "Roles", "Archive roles", "Allows archiving custom roles.", PermissionAllowedScopes.Organization),
        Entry("pg.roles.assign", "Roles", "Assign roles", "Allows assigning roles in a valid scope.", PermissionAllowedScopes.OrganizationAndProject),

        Entry("pg.permissions.view", "Permissions", "View permissions", "Allows reading visible permission definitions.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.permissions.create", "Permissions", "Create permissions", "Allows creating custom permission definitions.", PermissionAllowedScopes.Organization),
        Entry("pg.permissions.update", "Permissions", "Update permissions", "Allows updating custom permission definitions.", PermissionAllowedScopes.Organization),
        Entry("pg.permissions.archive", "Permissions", "Archive permissions", "Allows archiving custom permission definitions.", PermissionAllowedScopes.Organization),

        Entry("pg.access_requests.create", "Access requests", "Create access requests", "Allows creating access requests.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.access_requests.view_own", "Access requests", "View own access requests", "Allows reading access requests created by the actor.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.access_requests.view_all", "Access requests", "View all access requests", "Allows reading access requests for a valid scope.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.access_requests.review", "Access requests", "Review access requests", "Allows approving or denying access requests.", PermissionAllowedScopes.OrganizationAndProject),

        Entry("pg.authorization.check", "Authorization", "Check authorization", "Allows checking the actor's own authorization.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.authorization.check_other_users", "Authorization", "Check other users authorization", "Allows checking authorization for another user.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.authorization.explain_self", "Authorization", "Explain own authorization", "Allows explaining the actor's own authorization.", PermissionAllowedScopes.OrganizationAndProject),
        Entry("pg.authorization.explain_others", "Authorization", "Explain other users authorization", "Allows explaining authorization for another user.", PermissionAllowedScopes.OrganizationAndProject),

        Entry("pg.audit.view", "Audit", "View audit log", "Allows reading audit records.", PermissionAllowedScopes.Organization)
    ];

    public static PermissionDefinition ToPermissionDefinition(
        PlatformPermissionCatalogEntry entry,
        DateTimeOffset createdAtUtc)
    {
        return PermissionDefinition.CreatePlatform(
            entry.Id,
            entry.Key,
            entry.NormalizedKey,
            entry.DisplayName,
            entry.Description,
            entry.Module,
            entry.AllowedScopes,
            isRequestable: false,
            createdAtUtc);
    }

    private static PlatformPermissionCatalogEntry Entry(
        string key,
        string module,
        string displayName,
        string description,
        PermissionAllowedScopes allowedScopes)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        var legacyNormalizedKey = key.Trim().ToUpperInvariant();
        return new PlatformPermissionCatalogEntry(
            DeterministicGuid($"permission:{legacyNormalizedKey}"),
            key,
            normalizedKey,
            displayName,
            description,
            module,
            allowedScopes);
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
}

internal sealed record PlatformPermissionCatalogEntry(
    Guid Id,
    string Key,
    string NormalizedKey,
    string DisplayName,
    string Description,
    string Module,
    PermissionAllowedScopes AllowedScopes);
