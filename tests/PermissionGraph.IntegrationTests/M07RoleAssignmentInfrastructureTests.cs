using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PermissionGraph.Domain.Authorization.Enums;
using PermissionGraph.Domain.RoleAssignments.Models;

namespace PermissionGraph.IntegrationTests;

public sealed class M07RoleAssignmentInfrastructureTests
{
    [Fact]
    public void RoleAssignments_AreMappedWithPersistenceSafeguards()
    {
        using var dbContext = CreateModelOnlyDbContext();

        var entity = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(RoleAssignment));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("RoleAssignments");
        entity.FindProperty(nameof(RoleAssignment.Version))!.IsConcurrencyToken.Should().BeTrue();
        entity.FindProperty(nameof(RoleAssignment.GrantReason))!.GetMaxLength().Should().Be(RoleAssignment.ReasonMaxLength);
        entity.FindProperty(nameof(RoleAssignment.RevokeReason))!.GetMaxLength().Should().Be(RoleAssignment.ReasonMaxLength);

        entity.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .Should()
            .Contain([
                "CK_RoleAssignments_Status",
                "CK_RoleAssignments_ScopeType",
                "CK_RoleAssignments_ExpirationAfterStart",
                "CK_RoleAssignments_OrganizationScopeId"
            ]);

        entity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Should()
            .Contain([
                "IX_RoleAssignments_UserId_RoleId_ScopeType_ScopeId",
                "IX_RoleAssignments_OrganizationId_UserId_ScopeType_ScopeId_Sta~",
                "IX_RoleAssignments_UserId_StartsAtUtc_ExpiresAtUtc_Status",
                "IX_RoleAssignments_ExpiresAtUtc_Status",
                "IX_RoleAssignments_OrganizationId_Status"
            ]);
    }

    [Fact]
    public void RoleAssignments_UseRestrictiveForeignKeysInsideOrganizationBoundary()
    {
        using var dbContext = CreateModelOnlyDbContext();

        var entity = dbContext.Model.FindEntityType(typeof(RoleAssignment));

        entity.Should().NotBeNull();
        var roleBoundaryProperties = new[]
        {
            nameof(RoleAssignment.RoleId),
            nameof(RoleAssignment.OrganizationId)
        };

        entity!.GetForeignKeys()
            .Should()
            .Contain(foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name).SequenceEqual(roleBoundaryProperties));
        entity.GetForeignKeys()
            .Should()
            .Contain(foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Any(property => property.Name == nameof(RoleAssignment.OrganizationId)));
    }

    [Fact]
    public void RoleAssignmentMigration_IsRegisteredForRoleAssignments()
    {
        var migrationType = typeof(PermissionGraph.Infrastructure.Data.Migrations.M07_AddRoleAssignments);
        var migrationAttribute = migrationType
            .GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
            .Cast<MigrationAttribute>()
            .SingleOrDefault();

        migrationAttribute.Should().NotBeNull();
        migrationAttribute!.Id.Should().Be("20260723190000_M07_AddRoleAssignments");
        migrationType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(method => method.Name)
            .Should()
            .Contain(["Up", "Down"]);
    }

    [Fact]
    public void AuthorizationDecisionCacheKey_IncludesPolicyAndSubjectVersions()
    {
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subjectUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var projectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var key = new AuthorizationDecisionCacheKey(
            organizationId,
            OrganizationPolicyVersion: 7,
            subjectUserId,
            SubjectAuthorizationVersion: 11,
            AuthorizationScopeType.Project,
            projectId,
            "pg.projects.view");

        key.ToString().Should().Be(
            "authz:v1:11111111-1111-1111-1111-111111111111:7:22222222-2222-2222-2222-222222222222:11:Project:33333333-3333-3333-3333-333333333333:pg.projects.view");
    }

    [Fact]
    public void Infrastructure_ContainsRedisCacheAndExpirationWorkerTypes()
    {
        var infrastructureAssembly = typeof(PermissionGraphDbContext).Assembly;

        infrastructureAssembly.GetType("PermissionGraph.Infrastructure.Services.Authorization.RedisAuthorizationDecisionCache")
            .Should()
            .NotBeNull();
        infrastructureAssembly.GetType("PermissionGraph.Infrastructure.Services.RoleAssignments.RoleAssignmentExpirationWorker")
            .Should()
            .NotBeNull();
        infrastructureAssembly.GetType("PermissionGraph.Infrastructure.Repos.RoleAssignments.EfRoleAssignmentRepository")
            .Should()
            .NotBeNull();
    }

    private static PermissionGraphDbContext CreateModelOnlyDbContext()
    {
        var options = new DbContextOptionsBuilder<PermissionGraphDbContext>()
            .UseNpgsql("Host=localhost;Database=permissiongraph_model_only;Username=permissiongraph;Password=permissiongraph")
            .Options;

        return new PermissionGraphDbContext(options);
    }
}
