#nullable disable

namespace PermissionGraph.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PermissionGraphDbContext))]
    [Migration("20260721160000_M05_PromoteRoles")]
    public partial class M05_PromoteRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Roles",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(
                """
                UPDATE "Roles"
                SET
                    "UpdatedAtUtc" = COALESCE("CreatedAtUtc", now()),
                    "ArchivedAtUtc" = CASE
                        WHEN "IsActive" = false AND "ArchivedAtUtc" IS NULL THEN COALESCE("CreatedAtUtc", now())
                        ELSE "ArchivedAtUtc"
                    END,
                    "Version" = CASE WHEN "Version" < 1 THEN 1 ELSE "Version" END;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "RolePermissions" role_permission
                USING "Roles" role, "PermissionDefinitions" permission
                WHERE role_permission."RoleId" = role."Id"
                  AND role_permission."PermissionId" = permission."Id"
                  AND role."NormalizedName" = 'ORGANIZATION ADMINISTRATOR'
                  AND role."ScopeType" = 'Organization'
                  AND permission."OrganizationId" IS NULL
                  AND permission."NormalizedKey" IN ('pg.organizations.archive', 'pg.organizations.transfer_ownership');
                """);

            migrationBuilder.Sql(
                """
                WITH platform_scope_seed("NormalizedKey", "AllowedScopes") AS (
                    VALUES
                        ('pg.projects.view', 'OrganizationAndProject'),
                        ('pg.projects.update', 'OrganizationAndProject'),
                        ('pg.projects.archive', 'OrganizationAndProject'),
                        ('pg.roles.view', 'OrganizationAndProject'),
                        ('pg.roles.assign', 'OrganizationAndProject'),
                        ('pg.permissions.view', 'OrganizationAndProject'),
                        ('pg.access_requests.create', 'OrganizationAndProject'),
                        ('pg.access_requests.view_own', 'OrganizationAndProject'),
                        ('pg.access_requests.view_all', 'OrganizationAndProject'),
                        ('pg.access_requests.review', 'OrganizationAndProject'),
                        ('pg.authorization.check', 'OrganizationAndProject'),
                        ('pg.authorization.check_other_users', 'OrganizationAndProject'),
                        ('pg.authorization.explain_self', 'OrganizationAndProject'),
                        ('pg.authorization.explain_others', 'OrganizationAndProject')
                )
                UPDATE "PermissionDefinitions" permission
                SET
                    "AllowedScopes" = seed."AllowedScopes",
                    "UpdatedAtUtc" = now(),
                    "Version" = CASE WHEN permission."Version" < 1 THEN 1 ELSE permission."Version" + 1 END
                FROM platform_scope_seed seed
                WHERE permission."OrganizationId" IS NULL
                  AND permission."NormalizedKey" = seed."NormalizedKey"
                  AND permission."AllowedScopes" <> seed."AllowedScopes";
                """);

            migrationBuilder.Sql(
                """
                WITH role_seed("Name", "NormalizedName", "Description", "ScopeType") AS (
                    VALUES
                        ('Project Contributor', 'PROJECT CONTRIBUTOR', 'System project contributor role.', 'Project'),
                        ('Project Viewer', 'PROJECT VIEWER', 'System project viewer role.', 'Project')
                )
                INSERT INTO "Roles" (
                    "Id",
                    "OrganizationId",
                    "Name",
                    "NormalizedName",
                    "Description",
                    "ScopeType",
                    "RoleType",
                    "IsRequestable",
                    "IsActive",
                    "CreatedAtUtc",
                    "UpdatedAtUtc",
                    "ArchivedAtUtc",
                    "Version")
                SELECT
                    (
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 7, 2) ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 5, 2) ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 3, 2) ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 1, 2) || '-' ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 11, 2) ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 9, 2) || '-' ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 15, 2) ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 13, 2) || '-' ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 17, 4) || '-' ||
                        substr(md5('role:' || organization."Id"::text || ':' || seed."Name"), 21, 12)
                    )::uuid,
                    organization."Id",
                    seed."Name",
                    seed."NormalizedName",
                    seed."Description",
                    seed."ScopeType",
                    'System',
                    false,
                    true,
                    organization."CreatedAtUtc",
                    organization."CreatedAtUtc",
                    NULL,
                    1
                FROM "Organizations" organization
                CROSS JOIN role_seed seed
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Roles" existing
                    WHERE existing."OrganizationId" = organization."Id"
                      AND existing."NormalizedName" = seed."NormalizedName"
                      AND existing."ScopeType" = seed."ScopeType");
                """);

            migrationBuilder.Sql(
                """
                WITH role_permission_seed("RoleName", "ScopeType", "PermissionKey") AS (
                    VALUES
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.view'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.create'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'pg.organizations.update'),
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
                        ('PROJECT ADMINISTRATOR', 'Project', 'pg.authorization.explain_others'),
                        ('PROJECT CONTRIBUTOR', 'Project', 'pg.projects.view'),
                        ('PROJECT CONTRIBUTOR', 'Project', 'pg.access_requests.create'),
                        ('PROJECT CONTRIBUTOR', 'Project', 'pg.access_requests.view_own'),
                        ('PROJECT CONTRIBUTOR', 'Project', 'pg.authorization.check'),
                        ('PROJECT CONTRIBUTOR', 'Project', 'pg.authorization.explain_self'),
                        ('PROJECT VIEWER', 'Project', 'pg.projects.view'),
                        ('PROJECT VIEWER', 'Project', 'pg.access_requests.create'),
                        ('PROJECT VIEWER', 'Project', 'pg.access_requests.view_own'),
                        ('PROJECT VIEWER', 'Project', 'pg.authorization.check'),
                        ('PROJECT VIEWER', 'Project', 'pg.authorization.explain_self')
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
                name: "CK_Roles_Lifecycle",
                table: "Roles",
                sql: "((\"IsActive\" = TRUE AND \"ArchivedAtUtc\" IS NULL) OR (\"IsActive\" = FALSE AND \"ArchivedAtUtc\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_RoleType",
                table: "Roles",
                sql: "\"RoleType\" IN ('System', 'Custom')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_ScopeType",
                table: "Roles",
                sql: "\"ScopeType\" IN ('Organization', 'Project')");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_OrganizationId_NormalizedName",
                table: "Roles",
                columns: new[] { "OrganizationId", "NormalizedName" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "ValidateRolePermissionTenantAndScope"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    role_organization_id uuid;
                    role_scope text;
                    permission_organization_id uuid;
                    permission_type text;
                    permission_scope text;
                BEGIN
                    SELECT "OrganizationId", "ScopeType"
                    INTO role_organization_id, role_scope
                    FROM "Roles"
                    WHERE "Id" = NEW."RoleId";

                    SELECT "OrganizationId", "PermissionType", "AllowedScopes"
                    INTO permission_organization_id, permission_type, permission_scope
                    FROM "PermissionDefinitions"
                    WHERE "Id" = NEW."PermissionId";

                    IF permission_type = 'Platform' AND permission_organization_id IS NOT NULL THEN
                        RAISE EXCEPTION 'Platform permission must be global.' USING ERRCODE = '23514';
                    END IF;

                    IF permission_type = 'Custom' AND permission_organization_id IS DISTINCT FROM role_organization_id THEN
                        RAISE EXCEPTION 'Custom permission must belong to the same organization as the role.' USING ERRCODE = '23514';
                    END IF;

                    IF role_scope = 'Organization' AND permission_scope NOT IN ('Organization', 'OrganizationAndProject') THEN
                        RAISE EXCEPTION 'Permission scope is incompatible with role scope.' USING ERRCODE = '23514';
                    END IF;

                    IF role_scope = 'Project' AND permission_scope NOT IN ('Project', 'OrganizationAndProject') THEN
                        RAISE EXCEPTION 'Permission scope is incompatible with role scope.' USING ERRCODE = '23514';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                DROP TRIGGER IF EXISTS "TR_RolePermissions_ValidateTenantAndScope" ON "RolePermissions";

                CREATE TRIGGER "TR_RolePermissions_ValidateTenantAndScope"
                BEFORE INSERT OR UPDATE ON "RolePermissions"
                FOR EACH ROW
                EXECUTE FUNCTION "ValidateRolePermissionTenantAndScope"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "TR_RolePermissions_ValidateTenantAndScope" ON "RolePermissions";
                DROP FUNCTION IF EXISTS "ValidateRolePermissionTenantAndScope";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Roles_OrganizationId_NormalizedName",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_Lifecycle",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_RoleType",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_ScopeType",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Roles");
        }
    }
}
