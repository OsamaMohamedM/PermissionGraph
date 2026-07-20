using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionGraph.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PermissionGraphDbContext))]
    [Migration("20260720222100_M04_AddPermissionCatalog")]
    public partial class M04_AddPermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PermissionDefinitions_OrganizationId_NormalizedKey_IsActive",
                table: "PermissionDefinitions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "PermissionDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "PermissionDefinitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PermissionDefinitions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(
                """
                UPDATE "PermissionDefinitions"
                SET
                    "NormalizedKey" = lower("NormalizedKey"),
                    "AllowedScopes" = CASE "AllowedScopes"
                        WHEN 'Organization,Project' THEN 'OrganizationAndProject'
                        ELSE "AllowedScopes"
                    END,
                    "UpdatedAtUtc" = COALESCE("CreatedAtUtc", now()),
                    "ArchivedAtUtc" = CASE
                        WHEN "IsActive" = false AND "ArchivedAtUtc" IS NULL THEN COALESCE("CreatedAtUtc", now())
                        ELSE "ArchivedAtUtc"
                    END,
                    "Version" = CASE WHEN "Version" < 1 THEN 1 ELSE "Version" END;
                """);

            migrationBuilder.Sql(
                """
                WITH permission_seed(
                    "Id",
                    "Key",
                    "NormalizedKey",
                    "DisplayName",
                    "Description",
                    "Module",
                    "AllowedScopes") AS (
                    VALUES
                        ('8ed4385a-3c66-db8f-cc96-a5831d9c9c78'::uuid, 'pg.organizations.view', 'pg.organizations.view', 'View organizations', 'Allows reading visible organization details.', 'Organizations', 'Organization'),
                        ('0b1741a7-5724-8785-0448-ba1bd7c73da8'::uuid, 'pg.organizations.create', 'pg.organizations.create', 'Create organizations', 'Allows creating organizations.', 'Organizations', 'Organization'),
                        ('ac8ad69a-ea90-51a9-1a67-3e6e62859694'::uuid, 'pg.organizations.update', 'pg.organizations.update', 'Update organizations', 'Allows updating organization details.', 'Organizations', 'Organization'),
                        ('100621d5-f0fd-8834-6cd9-31101f3319bb'::uuid, 'pg.organizations.archive', 'pg.organizations.archive', 'Archive organizations', 'Allows archiving organizations.', 'Organizations', 'Organization'),
                        ('b578c311-cc76-664c-09b3-9705a82d414e'::uuid, 'pg.organizations.transfer_ownership', 'pg.organizations.transfer_ownership', 'Transfer ownership', 'Allows transferring organization ownership.', 'Organizations', 'Organization'),
                        ('c1212ec2-9338-fed1-7dfd-388105f6bc9b'::uuid, 'pg.members.view', 'pg.members.view', 'View members', 'Allows reading organization membership details.', 'Members', 'Organization'),
                        ('a8fa27d4-2aa0-d61c-0a4f-ede1787b3ae1'::uuid, 'pg.members.manage', 'pg.members.manage', 'Manage members', 'Allows adding organization members.', 'Members', 'Organization'),
                        ('3da49a67-8bc0-c902-b49a-37eb52364d81'::uuid, 'pg.members.suspend', 'pg.members.suspend', 'Suspend members', 'Allows suspending organization members.', 'Members', 'Organization'),
                        ('9b4cb03f-9f14-b2ea-58ad-fa4c34452ecf'::uuid, 'pg.members.remove', 'pg.members.remove', 'Remove members', 'Allows removing organization members.', 'Members', 'Organization'),
                        ('feadeaee-269b-aa07-52e6-c31260b7ef31'::uuid, 'pg.projects.create', 'pg.projects.create', 'Create projects', 'Allows creating projects in an organization.', 'Projects', 'Organization'),
                        ('bde7c609-b95f-a9bd-3f9d-b3ad2caa39d4'::uuid, 'pg.projects.view', 'pg.projects.view', 'View projects', 'Allows reading visible project details.', 'Projects', 'OrganizationAndProject'),
                        ('fcb59b55-8ce5-3975-33ee-91ae62bb4e56'::uuid, 'pg.projects.update', 'pg.projects.update', 'Update projects', 'Allows updating project details.', 'Projects', 'OrganizationAndProject'),
                        ('e719d4a4-dffe-4fe9-4c80-172c54339a42'::uuid, 'pg.projects.archive', 'pg.projects.archive', 'Archive projects', 'Allows archiving projects.', 'Projects', 'OrganizationAndProject'),
                        ('ff11bf79-219d-fa42-c5a4-dbad213ca1bd'::uuid, 'pg.roles.view', 'pg.roles.view', 'View roles', 'Allows reading role definitions.', 'Roles', 'OrganizationAndProject'),
                        ('eb53637f-2440-de8c-fdfa-17756a174baf'::uuid, 'pg.roles.create', 'pg.roles.create', 'Create roles', 'Allows creating custom roles.', 'Roles', 'Organization'),
                        ('a89f04e6-e7e4-fc9d-8dd6-3241f76d6326'::uuid, 'pg.roles.update', 'pg.roles.update', 'Update roles', 'Allows updating custom roles.', 'Roles', 'Organization'),
                        ('a661eea2-5109-afec-e6ae-7fa82cbd442d'::uuid, 'pg.roles.archive', 'pg.roles.archive', 'Archive roles', 'Allows archiving custom roles.', 'Roles', 'Organization'),
                        ('475cd7dc-299c-91fe-8eb7-b21de8ed4ae0'::uuid, 'pg.roles.assign', 'pg.roles.assign', 'Assign roles', 'Allows assigning roles in a valid scope.', 'Roles', 'OrganizationAndProject'),
                        ('33a78d73-ca91-7a8c-793d-d69366882512'::uuid, 'pg.permissions.view', 'pg.permissions.view', 'View permissions', 'Allows reading visible permission definitions.', 'Permissions', 'OrganizationAndProject'),
                        ('aa0f2444-458d-4c51-5339-dbf7aad9fa93'::uuid, 'pg.permissions.create', 'pg.permissions.create', 'Create permissions', 'Allows creating custom permission definitions.', 'Permissions', 'Organization'),
                        ('521f7f21-f30f-84f9-1891-a495c6e22206'::uuid, 'pg.permissions.update', 'pg.permissions.update', 'Update permissions', 'Allows updating custom permission definitions.', 'Permissions', 'Organization'),
                        ('95fd23cc-9869-0e1a-016b-2868f353777f'::uuid, 'pg.permissions.archive', 'pg.permissions.archive', 'Archive permissions', 'Allows archiving custom permission definitions.', 'Permissions', 'Organization'),
                        ('1eb4665e-b0a2-459f-2102-efd854381c23'::uuid, 'pg.access_requests.create', 'pg.access_requests.create', 'Create access requests', 'Allows creating access requests.', 'Access requests', 'OrganizationAndProject'),
                        ('a6aba481-e9ad-5b68-cdcf-0d65a0b2040a'::uuid, 'pg.access_requests.view_own', 'pg.access_requests.view_own', 'View own access requests', 'Allows reading access requests created by the actor.', 'Access requests', 'OrganizationAndProject'),
                        ('58dceb92-6c96-2421-3e9c-c1356a760d2d'::uuid, 'pg.access_requests.view_all', 'pg.access_requests.view_all', 'View all access requests', 'Allows reading access requests for a valid scope.', 'Access requests', 'OrganizationAndProject'),
                        ('46fe65ac-9f66-0ade-23e1-8b760618e1c6'::uuid, 'pg.access_requests.review', 'pg.access_requests.review', 'Review access requests', 'Allows approving or denying access requests.', 'Access requests', 'OrganizationAndProject'),
                        ('7f80a139-130b-8db8-f44b-56ce6c1f2657'::uuid, 'pg.authorization.check', 'pg.authorization.check', 'Check authorization', 'Allows checking the actor''s own authorization.', 'Authorization', 'OrganizationAndProject'),
                        ('81b4de0c-23bb-8306-fbce-33823d76400c'::uuid, 'pg.authorization.check_other_users', 'pg.authorization.check_other_users', 'Check other users authorization', 'Allows checking authorization for another user.', 'Authorization', 'OrganizationAndProject'),
                        ('09ddf9a5-a26e-91e3-7956-5e7357c5f08f'::uuid, 'pg.authorization.explain_self', 'pg.authorization.explain_self', 'Explain own authorization', 'Allows explaining the actor''s own authorization.', 'Authorization', 'OrganizationAndProject'),
                        ('9524916d-a290-9bc8-1f36-23d759c952c7'::uuid, 'pg.authorization.explain_others', 'pg.authorization.explain_others', 'Explain other users authorization', 'Allows explaining authorization for another user.', 'Authorization', 'OrganizationAndProject'),
                        ('71923fb8-9307-de91-3e7a-53e706bda3de'::uuid, 'pg.audit.view', 'pg.audit.view', 'View audit log', 'Allows reading audit records.', 'Audit', 'Organization')
                )
                INSERT INTO "PermissionDefinitions" (
                    "Id",
                    "OrganizationId",
                    "Key",
                    "NormalizedKey",
                    "DisplayName",
                    "Description",
                    "Module",
                    "PermissionType",
                    "AllowedScopes",
                    "IsRequestable",
                    "IsActive",
                    "CreatedAtUtc",
                    "UpdatedAtUtc",
                    "ArchivedAtUtc",
                    "Version")
                SELECT
                    seed."Id",
                    NULL,
                    seed."Key",
                    seed."NormalizedKey",
                    seed."DisplayName",
                    seed."Description",
                    seed."Module",
                    'Platform',
                    seed."AllowedScopes",
                    false,
                    true,
                    now(),
                    now(),
                    NULL,
                    1
                FROM permission_seed seed
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "PermissionDefinitions" existing
                    WHERE existing."OrganizationId" IS NULL
                      AND existing."NormalizedKey" = seed."NormalizedKey");
                """);

            migrationBuilder.Sql(
                """
                WITH role_permission_seed("RoleName", "ScopeType", "PermissionKey") AS (
                    VALUES
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.create'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.update'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.archive'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.transfer_ownership'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.members.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.members.manage'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.members.suspend'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.members.remove'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.projects.create'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.projects.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.projects.update'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.projects.archive'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.roles.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.roles.create'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.roles.update'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.roles.archive'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.roles.assign'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.permissions.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.permissions.create'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.permissions.update'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.permissions.archive'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.access_requests.view_all'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.access_requests.review'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.authorization.check'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.authorization.check_other_users'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.authorization.explain_self'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.authorization.explain_others'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.audit.view'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.organizations.view'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.members.view'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.projects.view'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.access_requests.create'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.access_requests.view_own'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.authorization.check'),
                        ('ORGANIZATION MEMBER', 'Organization', 'pg.authorization.explain_self'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.projects.view'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.projects.update'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.projects.archive'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.roles.view'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.roles.assign'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.permissions.view'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.access_requests.view_all'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.access_requests.review'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.authorization.check'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.authorization.check_other_users'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.authorization.explain_self'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.authorization.explain_others')
                )
                INSERT INTO "RolePermissions" (
                    "RoleId",
                    "PermissionId",
                    "AddedAtUtc",
                    "AddedByUserId")
                SELECT
                    role."Id",
                    permission."Id",
                    now(),
                    organization."OwnerUserId"
                FROM "Organizations" organization
                JOIN "Roles" role
                    ON role."OrganizationId" = organization."Id"
                JOIN role_permission_seed seed
                    ON seed."RoleName" = role."NormalizedName"
                   AND seed."ScopeType" = role."ScopeType"
                JOIN "PermissionDefinitions" permission
                    ON permission."OrganizationId" IS NULL
                   AND permission."NormalizedKey" = seed."PermissionKey"
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "RolePermissions" existing
                    WHERE existing."RoleId" = role."Id"
                      AND existing."PermissionId" = permission."Id");
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PermissionDefinitions_AllowedScopes",
                table: "PermissionDefinitions",
                sql: "\"AllowedScopes\" IN ('Organization', 'Project', 'OrganizationAndProject')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PermissionDefinitions_CustomKeyPrefix",
                table: "PermissionDefinitions",
                sql: "(\"PermissionType\" <> 'Custom' OR \"Key\" NOT LIKE 'pg.%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PermissionDefinitions_Lifecycle",
                table: "PermissionDefinitions",
                sql: "((\"IsActive\" = TRUE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"ArchivedAtUtc\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PermissionDefinitions_PermissionType",
                table: "PermissionDefinitions",
                sql: "\"PermissionType\" IN ('Platform', 'Custom')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PermissionDefinitions_TypeOrganization",
                table: "PermissionDefinitions",
                sql: "((\"PermissionType\" = 'Platform' AND \"OrganizationId\" IS NULL) OR (\"PermissionType\" = 'Custom' AND \"OrganizationId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_Module_IsActive",
                table: "PermissionDefinitions",
                columns: new[] { "Module", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_OrganizationId_IsActive_Id",
                table: "PermissionDefinitions",
                columns: new[] { "OrganizationId", "IsActive", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_OrganizationId_NormalizedKey",
                table: "PermissionDefinitions",
                columns: new[] { "OrganizationId", "NormalizedKey" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_PermissionType_IsActive",
                table: "PermissionDefinitions",
                columns: new[] { "PermissionType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PermissionDefinitions_AllowedScopes",
                table: "PermissionDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PermissionDefinitions_CustomKeyPrefix",
                table: "PermissionDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PermissionDefinitions_Lifecycle",
                table: "PermissionDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PermissionDefinitions_PermissionType",
                table: "PermissionDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PermissionDefinitions_TypeOrganization",
                table: "PermissionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionDefinitions_Module_IsActive",
                table: "PermissionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionDefinitions_OrganizationId_IsActive_Id",
                table: "PermissionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionDefinitions_OrganizationId_NormalizedKey",
                table: "PermissionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionDefinitions_PermissionType_IsActive",
                table: "PermissionDefinitions");

            migrationBuilder.Sql(
                """
                UPDATE "PermissionDefinitions"
                SET
                    "NormalizedKey" = upper("NormalizedKey"),
                    "AllowedScopes" = CASE "AllowedScopes"
                        WHEN 'OrganizationAndProject' THEN 'Organization,Project'
                        ELSE "AllowedScopes"
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "PermissionDefinitions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PermissionDefinitions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PermissionDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_OrganizationId_NormalizedKey_IsActive",
                table: "PermissionDefinitions",
                columns: new[] { "OrganizationId", "NormalizedKey", "IsActive" });
        }
    }
}
