using FluentAssertions;
using PermissionGraph.Domain.Common;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Domain.Tests;

public sealed class PermissionDefinitionTests
{
    private static readonly Guid PermissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePlatform_InitializesActivePlatformPermission()
    {
        var permission = CreatePlatformPermission();

        permission.Id.Should().Be(PermissionId);
        permission.OrganizationId.Should().BeNull();
        permission.Key.Should().Be("pg.permissions.view");
        permission.NormalizedKey.Should().Be("PG.PERMISSIONS.VIEW");
        permission.DisplayName.Should().Be("View permissions");
        permission.Description.Should().Be("View the permission catalog.");
        permission.Module.Should().Be("Permissions");
        permission.PermissionType.Should().Be(PermissionType.Platform);
        permission.AllowedScopes.Should().Be(PermissionAllowedScopes.Organization);
        permission.IsRequestable.Should().BeFalse();
        permission.IsActive.Should().BeTrue();
        permission.CreatedAtUtc.Should().Be(Now);
        permission.UpdatedAtUtc.Should().Be(Now);
        permission.ArchivedAtUtc.Should().BeNull();
        permission.Version.Should().Be(0);
    }

    [Fact]
    public void CreateCustom_InitializesActiveCustomPermission()
    {
        var permission = CreateCustomPermission();

        permission.Id.Should().Be(PermissionId);
        permission.OrganizationId.Should().Be(OrganizationId);
        permission.Key.Should().Be("documents.approve");
        permission.NormalizedKey.Should().Be("DOCUMENTS.APPROVE");
        permission.DisplayName.Should().Be("Approve documents");
        permission.Description.Should().Be("Approve organization documents.");
        permission.Module.Should().Be("Documents");
        permission.PermissionType.Should().Be(PermissionType.Custom);
        permission.AllowedScopes.Should().Be(PermissionAllowedScopes.Project);
        permission.IsRequestable.Should().BeTrue();
        permission.IsActive.Should().BeTrue();
        permission.ArchivedAtUtc.Should().BeNull();
        permission.Version.Should().Be(0);
    }

    [Fact]
    public void CreatePlatform_AcceptsReservedPlatformPrefix()
    {
        var permission = CreatePlatformPermission(key: "pg.audit.view", normalizedKey: "PG.AUDIT.VIEW");

        permission.Key.Should().Be("pg.audit.view");
    }

    [Fact]
    public void CreateCustom_RejectsReservedPlatformPrefix()
    {
        var act = () => CreateCustomPermission(key: "pg.custom.view", normalizedKey: "PG.CUSTOM.VIEW");

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "custom_permission_reserved_prefix");
    }

    [Fact]
    public void CreateCustom_RejectsRequiredIdentifiers()
    {
        var emptyPermission = () => PermissionDefinition.CreateCustom(
            Guid.Empty,
            OrganizationId,
            "documents.view",
            "DOCUMENTS.VIEW",
            "View documents",
            null,
            "Documents",
            PermissionAllowedScopes.Organization,
            true,
            Now);
        var emptyOrganization = () => PermissionDefinition.CreateCustom(
            PermissionId,
            Guid.Empty,
            "documents.view",
            "DOCUMENTS.VIEW",
            "View documents",
            null,
            "Documents",
            PermissionAllowedScopes.Organization,
            true,
            Now);

        emptyPermission.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
        emptyOrganization.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Fact]
    public void CreatePlatform_RejectsRequiredIdentifier()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            Guid.Empty,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            "View audit",
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "invalid_identifier");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("Documents.View")]
    [InlineData("documents")]
    [InlineData("documents.")]
    [InlineData(".documents.view")]
    [InlineData("documents..view")]
    [InlineData("documents.view-all")]
    public void Create_RejectsInvalidKey(string key)
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            key,
            "NORMALIZED",
            "View audit",
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Create_RejectsKeyLongerThanMaximum()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            $"a.{new string('a', PermissionDefinition.KeyMaxLength)}",
            "NORMALIZED",
            "View audit",
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_key_length");
    }

    [Fact]
    public void Create_RejectsEmptyNormalizedKey()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "",
            "View audit",
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_normalized_key_required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Create_RejectsInvalidDisplayName(string displayName)
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            displayName,
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => new[] { "permission_display_name_required", "permission_display_name_length" }.Contains(exception.ErrorCode));
    }

    [Fact]
    public void Create_RejectsDisplayNameLongerThanMaximum()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            new string('a', PermissionDefinition.DisplayNameMaxLength + 1),
            null,
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_display_name_length");
    }

    [Fact]
    public void Create_RejectsDescriptionLongerThanMaximum()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            "View audit",
            new string('a', PermissionDefinition.DescriptionMaxLength + 1),
            "Audit",
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_description_length");
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Create_RejectsInvalidModule(string module)
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            "View audit",
            null,
            module,
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => new[] { "permission_module_required", "permission_module_length" }.Contains(exception.ErrorCode));
    }

    [Fact]
    public void Create_RejectsModuleLongerThanMaximum()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            "View audit",
            null,
            new string('a', PermissionDefinition.ModuleMaxLength + 1),
            PermissionAllowedScopes.Organization,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_module_length");
    }

    [Fact]
    public void Create_RejectsUndefinedAllowedScope()
    {
        var act = () => PermissionDefinition.CreatePlatform(
            PermissionId,
            "pg.audit.view",
            "PG.AUDIT.VIEW",
            "View audit",
            null,
            "Audit",
            (PermissionAllowedScopes)99,
            false,
            Now);

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_allowed_scope_invalid");
    }

    [Fact]
    public void UpdateMetadata_ChangesOnlyMutableFields()
    {
        var permission = CreateCustomPermission();
        var updatedAt = Now.AddMinutes(5);

        permission.UpdateMetadata("Review documents", "Review submitted documents.", "Reviews", false, updatedAt);

        permission.Key.Should().Be("documents.approve");
        permission.NormalizedKey.Should().Be("DOCUMENTS.APPROVE");
        permission.OrganizationId.Should().Be(OrganizationId);
        permission.PermissionType.Should().Be(PermissionType.Custom);
        permission.AllowedScopes.Should().Be(PermissionAllowedScopes.Project);
        permission.DisplayName.Should().Be("Review documents");
        permission.Description.Should().Be("Review submitted documents.");
        permission.Module.Should().Be("Reviews");
        permission.IsRequestable.Should().BeFalse();
        permission.UpdatedAtUtc.Should().Be(updatedAt);
        permission.Version.Should().Be(0);
    }

    [Fact]
    public void UpdateMetadata_RejectsPlatformPermission()
    {
        var permission = CreatePlatformPermission();

        var act = () => permission.UpdateMetadata("Rename", null, "Permissions", false, Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "platform_permission_immutable");
    }

    [Fact]
    public void Archive_MarksCustomPermissionArchived()
    {
        var permission = CreateCustomPermission();
        var archivedAt = Now.AddMinutes(5);

        permission.Archive(archivedAt);

        permission.IsActive.Should().BeFalse();
        permission.ArchivedAtUtc.Should().Be(archivedAt);
        permission.UpdatedAtUtc.Should().Be(archivedAt);
        permission.Version.Should().Be(0);
    }

    [Fact]
    public void Archive_RejectsPlatformPermission()
    {
        var permission = CreatePlatformPermission();

        var act = () => permission.Archive(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "platform_permission_immutable");
    }

    [Fact]
    public void Archive_RejectsRepeatedArchive()
    {
        var permission = CreateCustomPermission();
        permission.Archive(Now.AddMinutes(1));

        var act = () => permission.Archive(Now.AddMinutes(2));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_already_archived");
    }

    [Fact]
    public void Activate_ReactivatesArchivedCustomPermission()
    {
        var permission = CreateCustomPermission();
        permission.Archive(Now.AddMinutes(1));
        var activatedAt = Now.AddMinutes(2);

        permission.Activate(activatedAt);

        permission.IsActive.Should().BeTrue();
        permission.ArchivedAtUtc.Should().BeNull();
        permission.UpdatedAtUtc.Should().Be(activatedAt);
        permission.Version.Should().Be(0);
    }

    [Fact]
    public void Activate_RejectsPlatformPermission()
    {
        var permission = CreatePlatformPermission();

        var act = () => permission.Activate(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "platform_permission_immutable");
    }

    [Fact]
    public void Activate_RejectsAlreadyActivePermission()
    {
        var permission = CreateCustomPermission();

        var act = () => permission.Activate(Now.AddMinutes(1));

        act.Should().Throw<DomainRuleViolationException>()
            .Where(exception => exception.ErrorCode == "permission_already_active");
    }

    private static PermissionDefinition CreatePlatformPermission(
        string key = "pg.permissions.view",
        string normalizedKey = "PG.PERMISSIONS.VIEW")
    {
        return PermissionDefinition.CreatePlatform(
            PermissionId,
            key,
            normalizedKey,
            "View permissions",
            "View the permission catalog.",
            "Permissions",
            PermissionAllowedScopes.Organization,
            false,
            Now);
    }

    private static PermissionDefinition CreateCustomPermission(
        string key = "documents.approve",
        string normalizedKey = "DOCUMENTS.APPROVE")
    {
        return PermissionDefinition.CreateCustom(
            PermissionId,
            OrganizationId,
            key,
            normalizedKey,
            "Approve documents",
            "Approve organization documents.",
            "Documents",
            PermissionAllowedScopes.Project,
            true,
            Now);
    }
}
