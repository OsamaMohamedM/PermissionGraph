using System.Text.RegularExpressions;
using PermissionGraph.Domain.Common;

namespace PermissionGraph.Domain.Permissions;

public sealed partial class PermissionDefinition
{
    public const int KeyMinLength = 3;
    public const int KeyMaxLength = 120;
    public const int DisplayNameMinLength = 3;
    public const int DisplayNameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
    public const int ModuleMinLength = 2;
    public const int ModuleMaxLength = 80;
    public const string ReservedPlatformPrefix = "pg.";

    private PermissionDefinition(
        Guid id,
        Guid? organizationId,
        string key,
        string normalizedKey,
        string displayName,
        string? description,
        string module,
        PermissionType permissionType,
        PermissionAllowedScopes allowedScopes,
        bool isRequestable,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Key = key;
        NormalizedKey = normalizedKey;
        DisplayName = displayName;
        Description = description;
        Module = module;
        PermissionType = permissionType;
        AllowedScopes = allowedScopes;
        IsRequestable = isRequestable;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private PermissionDefinition()
    {
        Key = string.Empty;
        NormalizedKey = string.Empty;
        DisplayName = string.Empty;
        Module = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid? OrganizationId { get; private set; }

    public string Key { get; private set; }

    public string NormalizedKey { get; private set; }

    public string DisplayName { get; private set; }

    public string? Description { get; private set; }

    public string Module { get; private set; }

    public PermissionType PermissionType { get; private set; }

    public PermissionAllowedScopes AllowedScopes { get; private set; }

    public bool IsRequestable { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public static PermissionDefinition CreatePlatform(
        Guid id,
        string key,
        string normalizedKey,
        string displayName,
        string? description,
        string module,
        PermissionAllowedScopes allowedScopes,
        bool isRequestable,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureKey(key);
        EnsureNormalizedKey(normalizedKey);
        EnsureMetadata(displayName, description, module);
        EnsureAllowedScopes(allowedScopes);

        return new PermissionDefinition(
            id,
            organizationId: null,
            key,
            normalizedKey,
            displayName,
            description,
            module,
            PermissionType.Platform,
            allowedScopes,
            isRequestable,
            createdAtUtc);
    }

    public static PermissionDefinition CreateCustom(
        Guid id,
        Guid organizationId,
        string key,
        string normalizedKey,
        string displayName,
        string? description,
        string module,
        PermissionAllowedScopes allowedScopes,
        bool isRequestable,
        DateTimeOffset createdAtUtc)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureKey(key);
        EnsureCustomKey(key);
        EnsureNormalizedKey(normalizedKey);
        EnsureMetadata(displayName, description, module);
        EnsureAllowedScopes(allowedScopes);

        return new PermissionDefinition(
            id,
            organizationId,
            key,
            normalizedKey,
            displayName,
            description,
            module,
            PermissionType.Custom,
            allowedScopes,
            isRequestable,
            createdAtUtc);
    }

    public void UpdateMetadata(
        string displayName,
        string? description,
        string module,
        bool isRequestable,
        DateTimeOffset updatedAtUtc)
    {
        EnsureCustomMutation();
        EnsureMetadata(displayName, description, module);

        DisplayName = displayName;
        Description = description;
        Module = module;
        IsRequestable = isRequestable;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        EnsureCustomMutation();

        if (!IsActive)
        {
            throw new DomainRuleViolationException(
                "permission_already_archived",
                "Archived permission cannot be archived again.");
        }

        IsActive = false;
        ArchivedAtUtc = archivedAtUtc;
        UpdatedAtUtc = archivedAtUtc;
    }

    public void Activate(DateTimeOffset activatedAtUtc)
    {
        EnsureCustomMutation();

        if (IsActive)
        {
            throw new DomainRuleViolationException(
                "permission_already_active",
                "Active permission cannot be activated again.");
        }

        IsActive = true;
        ArchivedAtUtc = null;
        UpdatedAtUtc = activatedAtUtc;
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invalid_identifier",
                $"{parameterName} is required.");
        }
    }

    private static void EnsureKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainRuleViolationException("permission_key_required", "Permission key is required.");
        }

        if (key.Length is < KeyMinLength or > KeyMaxLength)
        {
            throw new DomainRuleViolationException("permission_key_length", "Permission key length is invalid.");
        }

        if (!PermissionKeyRegex().IsMatch(key))
        {
            throw new DomainRuleViolationException("permission_key_format", "Permission key format is invalid.");
        }
    }

    private static void EnsureCustomKey(string key)
    {
        if (key.StartsWith(ReservedPlatformPrefix, StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException(
                "custom_permission_reserved_prefix",
                "Custom permission key cannot use the reserved platform prefix.");
        }
    }

    private static void EnsureNormalizedKey(string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new DomainRuleViolationException(
                "permission_normalized_key_required",
                "Normalized permission key is required.");
        }
    }

    private static void EnsureMetadata(string displayName, string? description, string module)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainRuleViolationException(
                "permission_display_name_required",
                "Permission display name is required.");
        }

        if (displayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            throw new DomainRuleViolationException(
                "permission_display_name_length",
                "Permission display name length is invalid.");
        }

        if (description?.Length > DescriptionMaxLength)
        {
            throw new DomainRuleViolationException(
                "permission_description_length",
                "Permission description length is invalid.");
        }

        if (string.IsNullOrWhiteSpace(module))
        {
            throw new DomainRuleViolationException(
                "permission_module_required",
                "Permission module is required.");
        }

        if (module.Length is < ModuleMinLength or > ModuleMaxLength)
        {
            throw new DomainRuleViolationException(
                "permission_module_length",
                "Permission module length is invalid.");
        }
    }

    private static void EnsureAllowedScopes(PermissionAllowedScopes allowedScopes)
    {
        if (!Enum.IsDefined(allowedScopes))
        {
            throw new DomainRuleViolationException(
                "permission_allowed_scope_invalid",
                "Permission allowed scope is invalid.");
        }
    }

    private void EnsureCustomMutation()
    {
        if (PermissionType == PermissionType.Platform)
        {
            throw new DomainRuleViolationException(
                "platform_permission_immutable",
                "Platform permissions cannot be mutated by tenant operations.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionKeyRegex();
}
