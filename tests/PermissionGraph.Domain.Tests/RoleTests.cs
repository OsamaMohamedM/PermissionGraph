namespace PermissionGraph.Domain.Tests;

public sealed class RoleTests
{
    private static readonly Guid RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CloneRoleId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherOrganizationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OrganizationPermissionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ProjectPermissionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateCustom_InitializesActiveOrganizationRole()
    {
        var role = CreateCustomOrganizationRole();

        role.Id.Should().Be(RoleId);
        role.OrganizationId.Should().Be(OrganizationId);
        role.Name.Should().Be("Document Manager");
        role.NormalizedName.Should().Be("DOCUMENT MANAGER");
        role.Description.Should().Be("Manages document permissions.");
        role.ScopeType.Should().Be(RoleScopeType.Organization);
        role.RoleType.Should().Be(RoleType.Custom);
        role.IsRequestable.Should().BeTrue();
        role.IsActive.Should().BeTrue();
        role.CreatedAtUtc.Should().Be(Now);
        role.UpdatedAtUtc.Should().Be(Now);
        role.ArchivedAtUtc.Should().BeNull();
        role.Version.Should().Be(0);
        role.Permissions.Should().ContainSingle(permission => permission.PermissionId == OrganizationPermissionId);
    }

    [Fact]
    public void CreateCustom_InitializesActiveProjectRole()
    {
        var role = Role.CreateCustom(
            RoleId,
            OrganizationId,
            "Project Reviewer",
            "PROJECT REVIEWER",
            null,
            RoleScopeType.Project,
            isRequestable: false,
            Now,
            [ProjectPermission()],
            ActorUserId);

        role.ScopeType.Should().Be(RoleScopeType.Project);
        role.RoleType.Should().Be(RoleType.Custom);
        role.Permissions.Should().ContainSingle(permission => permission.PermissionId == ProjectPermissionId);
    }

    [Fact]
    public void CreateSystem_MaterializesProtectedSystemRole()
    {
        var role = CreateSystemOrganizationRole();

        role.RoleType.Should().Be(RoleType.System);
        role.ScopeType.Should().Be(RoleScopeType.Organization);
        role.IsActive.Should().BeTrue();
        role.IsRequestable.Should().BeFalse();
        role.Permissions.Should().ContainSingle(permission => permission.PermissionId == OrganizationPermissionId);
        role.Version.Should().Be(0);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000")]
    public void CreateCustom_RejectsRequiredIdentifiers(string roleId, string organizationId)
    {
        var act = () => Role.CreateCustom(
            Guid.Parse(roleId),
            Guid.Parse(organizationId),
            "Document Manager",
            "DOCUMENT MANAGER",
            null,
            RoleScopeType.Organization,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void CreateCustom_RejectsInvalidName(string name)
    {
        var act = () => Role.CreateCustom(
            RoleId,
            OrganizationId,
            name,
            "DOCUMENT MANAGER",
            null,
            RoleScopeType.Organization,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void CreateCustom_RejectsNameLongerThanMaximum()
    {
        var act = () => Role.CreateCustom(
            RoleId,
            OrganizationId,
            new string('a', Role.NameMaxLength + 1),
            "DOCUMENT MANAGER",
            null,
            RoleScopeType.Organization,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_name_length");
    }

    [Fact]
    public void CreateCustom_RejectsEmptyNormalizedName()
    {
        var act = () => Role.CreateCustom(
            RoleId,
            OrganizationId,
            "Document Manager",
            "",
            null,
            RoleScopeType.Organization,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_normalized_name_required");
    }

    [Fact]
    public void CreateCustom_RejectsDescriptionLongerThanMaximum()
    {
        var act = () => Role.CreateCustom(
            RoleId,
            OrganizationId,
            "Document Manager",
            "DOCUMENT MANAGER",
            new string('a', Role.DescriptionMaxLength + 1),
            RoleScopeType.Organization,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_description_length");
    }

    [Fact]
    public void CreateCustom_RejectsInvalidScopeType()
    {
        var act = () => Role.CreateCustom(
            RoleId,
            OrganizationId,
            "Document Manager",
            "DOCUMENT MANAGER",
            null,
            (RoleScopeType)99,
            true,
            Now,
            [],
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_scope_invalid");
    }

    [Fact]
    public void UpdateMetadata_ChangesOnlyMutableCustomRoleFields()
    {
        var role = CreateCustomOrganizationRole();
        var updatedAt = Now.AddMinutes(5);

        role.UpdateMetadata("Document Owner", "DOCUMENT OWNER", "Updated.", false, updatedAt);

        role.Name.Should().Be("Document Owner");
        role.NormalizedName.Should().Be("DOCUMENT OWNER");
        role.Description.Should().Be("Updated.");
        role.IsRequestable.Should().BeFalse();
        role.OrganizationId.Should().Be(OrganizationId);
        role.ScopeType.Should().Be(RoleScopeType.Organization);
        role.RoleType.Should().Be(RoleType.Custom);
        role.UpdatedAtUtc.Should().Be(updatedAt);
        role.Version.Should().Be(0);
    }

    [Fact]
    public void UpdateMetadata_RejectsSystemRole()
    {
        var role = CreateSystemOrganizationRole();

        var act = () => role.UpdateMetadata("Rename", "RENAME", null, false, Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
    }

    [Fact]
    public void Archive_MarksCustomRoleArchived()
    {
        var role = CreateCustomOrganizationRole();
        var archivedAt = Now.AddMinutes(5);

        role.Archive(archivedAt);

        role.IsActive.Should().BeFalse();
        role.ArchivedAtUtc.Should().Be(archivedAt);
        role.UpdatedAtUtc.Should().Be(archivedAt);
        role.Version.Should().Be(0);
    }

    [Fact]
    public void Archive_RejectsRepeatedArchive()
    {
        var role = CreateCustomOrganizationRole();
        role.Archive(Now.AddMinutes(1));

        var act = () => role.Archive(Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_already_archived");
    }

    [Fact]
    public void Archive_RejectsSystemRole()
    {
        var role = CreateSystemOrganizationRole();

        var act = () => role.Archive(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
    }

    [Fact]
    public void Archive_RejectsActiveOrScheduledAssignmentsWhenCallerProvidesCounts()
    {
        var role = CreateCustomOrganizationRole();

        var act = () => role.Archive(Now.AddMinutes(1), activeAssignmentCount: 1, scheduledAssignmentCount: 0);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_has_active_or_scheduled_assignments");
    }

    [Fact]
    public void Activate_ReactivatesArchivedCustomRole()
    {
        var role = CreateCustomOrganizationRole();
        role.Archive(Now.AddMinutes(1));
        var activatedAt = Now.AddMinutes(2);

        role.Activate(activatedAt);

        role.IsActive.Should().BeTrue();
        role.ArchivedAtUtc.Should().BeNull();
        role.UpdatedAtUtc.Should().Be(activatedAt);
        role.Version.Should().Be(0);
    }

    [Fact]
    public void Activate_RejectsRepeatedActivation()
    {
        var role = CreateCustomOrganizationRole();

        var act = () => role.Activate(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_already_active");
    }

    [Fact]
    public void Activate_RejectsSystemRole()
    {
        var role = CreateSystemOrganizationRole();

        var act = () => role.Activate(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
    }

    [Fact]
    public void CloneAsCustom_CopiesAllowedRoleIntoNewCustomRole()
    {
        var source = CreateSystemOrganizationRole();
        var clonedAt = Now.AddMinutes(5);

        var clone = source.CloneAsCustom(
            CloneRoleId,
            "Copied Administrator",
            "COPIED ADMINISTRATOR",
            "Copied from system role.",
            isRequestable: true,
            clonedAt,
            ActorUserId);

        clone.Id.Should().Be(CloneRoleId);
        clone.OrganizationId.Should().Be(source.OrganizationId);
        clone.Name.Should().Be("Copied Administrator");
        clone.NormalizedName.Should().Be("COPIED ADMINISTRATOR");
        clone.Description.Should().Be("Copied from system role.");
        clone.ScopeType.Should().Be(source.ScopeType);
        clone.RoleType.Should().Be(RoleType.Custom);
        clone.IsRequestable.Should().BeTrue();
        clone.IsActive.Should().BeTrue();
        clone.CreatedAtUtc.Should().Be(clonedAt);
        clone.UpdatedAtUtc.Should().Be(clonedAt);
        clone.Version.Should().Be(0);
        clone.Permissions.Select(permission => permission.PermissionId)
            .Should()
            .BeEquivalentTo(source.Permissions.Select(permission => permission.PermissionId));
        clone.Permissions.Should().OnlyContain(permission => permission.RoleId == CloneRoleId);
    }

    [Fact]
    public void CloneAsCustom_RejectsArchivedSourceRole()
    {
        var source = CreateCustomOrganizationRole();
        source.Archive(Now.AddMinutes(1));

        var act = () => source.CloneAsCustom(
            CloneRoleId,
            "Copied Role",
            "COPIED ROLE",
            null,
            false,
            Now.AddMinutes(2),
            ActorUserId);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "archived_role_cannot_be_cloned");
    }

    [Fact]
    public void ReplacePermissions_ReplacesCustomRoleMappings()
    {
        var role = CreateCustomOrganizationRole();
        var updatedAt = Now.AddMinutes(5);

        role.ReplacePermissions([OrganizationAndProjectPermission()], ActorUserId, updatedAt);

        role.Permissions.Should().ContainSingle(permission => permission.PermissionId == ProjectPermissionId);
        role.UpdatedAtUtc.Should().Be(updatedAt);
        role.Version.Should().Be(0);
    }

    [Fact]
    public void ReplacePermissions_RejectsDuplicatePermissionIds()
    {
        var role = CreateCustomOrganizationRole();
        var permission = OrganizationPermission();

        var act = () => role.ReplacePermissions([permission, permission], ActorUserId, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_permission_duplicate");
    }

    [Fact]
    public void ReplacePermissions_RejectsScopeIncompatiblePermission()
    {
        var role = CreateCustomOrganizationRole();

        var act = () => role.ReplacePermissions([ProjectPermission()], ActorUserId, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_permission_scope_incompatible");
    }

    [Fact]
    public void ReplacePermissions_RejectsInactivePermission()
    {
        var role = CreateCustomOrganizationRole();
        var permission = OrganizationPermission();
        permission.Archive(Now.AddMinutes(1));

        var act = () => role.ReplacePermissions([permission], ActorUserId, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_permission_inactive");
    }

    [Fact]
    public void ReplacePermissions_RejectsCrossTenantCustomPermission()
    {
        var role = CreateCustomOrganizationRole();

        var act = () => role.ReplacePermissions([OrganizationPermission(OtherOrganizationId)], ActorUserId, Now.AddMinutes(5));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "role_permission_cross_tenant");
    }

    [Fact]
    public void ReplacePermissions_RejectsSystemRole()
    {
        var role = CreateSystemOrganizationRole();

        var act = () => role.ReplacePermissions([OrganizationPermission()], ActorUserId, Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "system_role_protected");
    }

    private static Role CreateCustomOrganizationRole()
    {
        return Role.CreateCustom(
            RoleId,
            OrganizationId,
            "Document Manager",
            "DOCUMENT MANAGER",
            "Manages document permissions.",
            RoleScopeType.Organization,
            true,
            Now,
            [OrganizationPermission()],
            ActorUserId);
    }

    private static Role CreateSystemOrganizationRole()
    {
        return Role.CreateSystem(
            RoleId,
            OrganizationId,
            "Organization Administrator",
            "ORGANIZATION ADMINISTRATOR",
            "System organization administrator role.",
            RoleScopeType.Organization,
            false,
            Now,
            [OrganizationPermission()],
            ActorUserId);
    }

    private static PermissionDefinition OrganizationPermission(Guid? organizationId = null)
    {
        return PermissionDefinition.CreateCustom(
            OrganizationPermissionId,
            organizationId ?? OrganizationId,
            "documents.manage",
            "DOCUMENTS.MANAGE",
            "Manage documents",
            null,
            "Documents",
            PermissionAllowedScopes.Organization,
            true,
            Now);
    }

    private static PermissionDefinition ProjectPermission()
    {
        return PermissionDefinition.CreateCustom(
            ProjectPermissionId,
            OrganizationId,
            "documents.review",
            "DOCUMENTS.REVIEW",
            "Review documents",
            null,
            "Documents",
            PermissionAllowedScopes.Project,
            true,
            Now);
    }

    private static PermissionDefinition OrganizationAndProjectPermission()
    {
        return PermissionDefinition.CreatePlatform(
            ProjectPermissionId,
            "pg.projects.view",
            "PG.PROJECTS.VIEW",
            "View projects",
            null,
            "Projects",
            PermissionAllowedScopes.OrganizationAndProject,
            false,
            Now);
    }
}
