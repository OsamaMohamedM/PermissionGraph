namespace PermissionGraph.Domain.Roles.Models;

public sealed class Role
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 1000;

    private readonly List<RolePermission> _permissions = [];

    private Role(
        Guid id,
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        RoleScopeType scopeType,
        RoleType roleType,
        bool isRequestable,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        ScopeType = scopeType;
        RoleType = roleType;
        IsRequestable = isRequestable;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private Role()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public RoleScopeType ScopeType { get; private set; }

    public RoleType RoleType { get; private set; }

    public bool IsRequestable { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public static Role CreateCustom(
        Guid id,
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        RoleScopeType scopeType,
        bool isRequestable,
        DateTimeOffset createdAtUtc,
        IEnumerable<PermissionDefinition> permissions,
        Guid addedByUserId)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureMetadata(name, normalizedName, description);
        EnsureScopeType(scopeType);

        var role = new Role(
            id,
            organizationId,
            name,
            normalizedName,
            description,
            scopeType,
            RoleType.Custom,
            isRequestable,
            createdAtUtc);

        role.SetPermissionMappings(permissions, addedByUserId, createdAtUtc);
        return role;
    }

    public static Role CreateSystem(
        Guid id,
        Guid organizationId,
        string name,
        string normalizedName,
        string? description,
        RoleScopeType scopeType,
        bool isRequestable,
        DateTimeOffset createdAtUtc,
        IEnumerable<PermissionDefinition> permissions,
        Guid addedByUserId)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureMetadata(name, normalizedName, description);
        EnsureScopeType(scopeType);

        var role = new Role(
            id,
            organizationId,
            name,
            normalizedName,
            description,
            scopeType,
            RoleType.System,
            isRequestable,
            createdAtUtc);

        role.SetPermissionMappings(permissions, addedByUserId, createdAtUtc);
        return role;
    }

    public void UpdateMetadata(
        string name,
        string normalizedName,
        string? description,
        bool isRequestable,
        DateTimeOffset updatedAtUtc)
    {
        EnsureTenantMutableCustomRole();
        EnsureMetadata(name, normalizedName, description);

        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        IsRequestable = isRequestable;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ReplacePermissions(
        IEnumerable<PermissionDefinition> permissions,
        Guid addedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        EnsureTenantMutableCustomRole();
        SetPermissionMappings(permissions, addedByUserId, updatedAtUtc);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Archive(
        DateTimeOffset archivedAtUtc,
        int activeAssignmentCount = 0,
        int scheduledAssignmentCount = 0)
    {
        EnsureTenantMutableCustomRole();
        EnsureAssignmentCounts(activeAssignmentCount, scheduledAssignmentCount);

        if (!IsActive)
        {
            throw new DomainRuleViolationException(
                "role_already_archived",
                "Archived role cannot be archived again.");
        }

        if (activeAssignmentCount > 0 || scheduledAssignmentCount > 0)
        {
            throw new DomainRuleViolationException(
                "role_has_active_or_scheduled_assignments",
                "Role with active or scheduled assignments cannot be archived.");
        }

        IsActive = false;
        ArchivedAtUtc = archivedAtUtc;
        UpdatedAtUtc = archivedAtUtc;
    }

    public void Activate(DateTimeOffset activatedAtUtc)
    {
        EnsureTenantMutableCustomRole();

        if (IsActive)
        {
            throw new DomainRuleViolationException(
                "role_already_active",
                "Active role cannot be activated again.");
        }

        IsActive = true;
        ArchivedAtUtc = null;
        UpdatedAtUtc = activatedAtUtc;
    }

    public Role CloneAsCustom(
        Guid id,
        string name,
        string normalizedName,
        string? description,
        bool isRequestable,
        DateTimeOffset createdAtUtc,
        Guid addedByUserId)
    {
        EnsureNotEmpty(id, nameof(id));
        EnsureNotEmpty(addedByUserId, nameof(addedByUserId));
        EnsureMetadata(name, normalizedName, description);

        if (!IsActive)
        {
            throw new DomainRuleViolationException(
                "archived_role_cannot_be_cloned",
                "Archived role cannot be cloned.");
        }

        var clone = new Role(
            id,
            OrganizationId,
            name,
            normalizedName,
            description,
            ScopeType,
            RoleType.Custom,
            isRequestable,
            createdAtUtc);

        foreach (var permission in _permissions)
        {
            clone._permissions.Add(permission.CopyForRole(id, createdAtUtc, addedByUserId));
        }

        return clone;
    }

    private void SetPermissionMappings(
        IEnumerable<PermissionDefinition> permissions,
        Guid addedByUserId,
        DateTimeOffset addedAtUtc)
    {
        EnsureNotEmpty(addedByUserId, nameof(addedByUserId));

        var permissionList = permissions?.ToArray()
            ?? throw new DomainRuleViolationException("role_permissions_required", "Role permissions are required.");
        var duplicatePermission = permissionList
            .GroupBy(permission => permission.Id)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1);

        if (duplicatePermission is not null)
        {
            throw new DomainRuleViolationException(
                "role_permission_duplicate",
                "Role permission mappings cannot contain duplicate or empty permission identifiers.");
        }

        foreach (var permission in permissionList)
        {
            EnsurePermissionCanBelongToRole(permission);
        }

        var existingByPermissionId = _permissions.ToDictionary(rolePermission => rolePermission.PermissionId);
        var replacementMappings = new List<RolePermission>(permissionList.Length);
        foreach (var permission in permissionList)
        {
            replacementMappings.Add(existingByPermissionId.TryGetValue(permission.Id, out var existing)
                ? existing
                : RolePermission.Create(Id, permission.Id, addedAtUtc, addedByUserId));
        }

        _permissions.Clear();
        _permissions.AddRange(replacementMappings);
    }

    private void EnsurePermissionCanBelongToRole(PermissionDefinition permission)
    {
        if (!permission.IsActive)
        {
            throw new DomainRuleViolationException(
                "role_permission_inactive",
                "Inactive permission cannot be assigned to a role.");
        }

        if (permission.PermissionType == PermissionType.Custom && permission.OrganizationId != OrganizationId)
        {
            throw new DomainRuleViolationException(
                "role_permission_cross_tenant",
                "Custom permission must belong to the same organization as the role.");
        }

        if (permission.PermissionType == PermissionType.Platform && permission.OrganizationId is not null)
        {
            throw new DomainRuleViolationException(
                "role_permission_invalid_platform_tenant",
                "Platform permission must be global.");
        }

        if (!IsScopeCompatible(ScopeType, permission.AllowedScopes))
        {
            throw new DomainRuleViolationException(
                "role_permission_scope_incompatible",
                "Permission scope is incompatible with the role scope.");
        }
    }

    private static bool IsScopeCompatible(RoleScopeType roleScopeType, PermissionAllowedScopes permissionAllowedScopes)
    {
        return roleScopeType switch
        {
            RoleScopeType.Organization => permissionAllowedScopes is PermissionAllowedScopes.Organization or PermissionAllowedScopes.OrganizationAndProject,
            RoleScopeType.Project => permissionAllowedScopes is PermissionAllowedScopes.Project or PermissionAllowedScopes.OrganizationAndProject,
            _ => false
        };
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

    private static void EnsureMetadata(string name, string normalizedName, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleViolationException("role_name_required", "Role name is required.");
        }

        if (name.Length is < NameMinLength or > NameMaxLength)
        {
            throw new DomainRuleViolationException("role_name_length", "Role name length is invalid.");
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new DomainRuleViolationException("role_normalized_name_required", "Normalized role name is required.");
        }

        if (description?.Length > DescriptionMaxLength)
        {
            throw new DomainRuleViolationException("role_description_length", "Role description length is invalid.");
        }
    }

    private static void EnsureScopeType(RoleScopeType scopeType)
    {
        if (!Enum.IsDefined(scopeType))
        {
            throw new DomainRuleViolationException("role_scope_invalid", "Role scope type is invalid.");
        }
    }

    private static void EnsureAssignmentCounts(int activeAssignmentCount, int scheduledAssignmentCount)
    {
        if (activeAssignmentCount < 0 || scheduledAssignmentCount < 0)
        {
            throw new DomainRuleViolationException(
                "role_assignment_count_invalid",
                "Role assignment counts cannot be negative.");
        }
    }

    private void EnsureTenantMutableCustomRole()
    {
        if (RoleType == RoleType.System)
        {
            throw new DomainRuleViolationException(
                "system_role_protected",
                "System roles cannot be mutated by tenant operations.");
        }
    }
}
