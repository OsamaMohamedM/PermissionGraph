using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionGraph.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class M03_AddProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Roles_Id_OrganizationId",
                table: "Roles",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.UniqueConstraint("AK_Projects_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_Projects_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAdministratorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAdministratorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAdministratorAssignments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAdministratorAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAdministratorAssignments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAdministratorAssignments_Projects_ProjectId_Organiza~",
                        columns: x => new { x.ProjectId, x.OrganizationId },
                        principalTable: "Projects",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAdministratorAssignments_Roles_RoleId_OrganizationId",
                        columns: x => new { x.RoleId, x.OrganizationId },
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_CreatedByUserId",
                table: "ProjectAdministratorAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_OrganizationId_ProjectId",
                table: "ProjectAdministratorAssignments",
                columns: new[] { "OrganizationId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_OrganizationId_ProjectId_Us~",
                table: "ProjectAdministratorAssignments",
                columns: new[] { "OrganizationId", "ProjectId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_OrganizationId_UserId",
                table: "ProjectAdministratorAssignments",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_ProjectId_OrganizationId",
                table: "ProjectAdministratorAssignments",
                columns: new[] { "ProjectId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_RoleId_OrganizationId",
                table: "ProjectAdministratorAssignments",
                columns: new[] { "RoleId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAdministratorAssignments_UserId",
                table: "ProjectAdministratorAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_Id",
                table: "Projects",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_NormalizedName",
                table: "Projects",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_Status",
                table: "Projects",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.Sql(
                """
                WITH permission_seed("Key", "NormalizedKey", "DisplayName", "Module", "AllowedScopes") AS (
                    VALUES
                        ('pg.projects.create', 'PG.PROJECTS.CREATE', 'Create projects', 'Projects', 'Organization'),
                        ('pg.projects.view', 'PG.PROJECTS.VIEW', 'View projects', 'Projects', 'Organization,Project'),
                        ('pg.projects.update', 'PG.PROJECTS.UPDATE', 'Update projects', 'Projects', 'Project'),
                        ('pg.projects.archive', 'PG.PROJECTS.ARCHIVE', 'Archive projects', 'Projects', 'Project')
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
                    "CreatedAtUtc")
                SELECT
                    (substr(md5('permission:' || seed."NormalizedKey"), 1, 8) || '-' ||
                     substr(md5('permission:' || seed."NormalizedKey"), 9, 4) || '-' ||
                     substr(md5('permission:' || seed."NormalizedKey"), 13, 4) || '-' ||
                     substr(md5('permission:' || seed."NormalizedKey"), 17, 4) || '-' ||
                     substr(md5('permission:' || seed."NormalizedKey"), 21, 12))::uuid,
                    NULL,
                    seed."Key",
                    seed."NormalizedKey",
                    seed."DisplayName",
                    seed."DisplayName",
                    seed."Module",
                    'Platform',
                    seed."AllowedScopes",
                    false,
                    true,
                    now()
                FROM permission_seed seed
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "PermissionDefinitions" existing
                    WHERE existing."OrganizationId" IS NULL
                      AND existing."NormalizedKey" = seed."NormalizedKey");
                """);

            migrationBuilder.Sql(
                """
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
                    "CreatedAtUtc")
                SELECT
                    (substr(md5('role:' || organization."Id"::text || ':Project Administrator'), 1, 8) || '-' ||
                     substr(md5('role:' || organization."Id"::text || ':Project Administrator'), 9, 4) || '-' ||
                     substr(md5('role:' || organization."Id"::text || ':Project Administrator'), 13, 4) || '-' ||
                     substr(md5('role:' || organization."Id"::text || ':Project Administrator'), 17, 4) || '-' ||
                     substr(md5('role:' || organization."Id"::text || ':Project Administrator'), 21, 12))::uuid,
                    organization."Id",
                    'Project Administrator',
                    'PROJECT ADMINISTRATOR',
                    'System project administrator role.',
                    'Project',
                    'System',
                    false,
                    true,
                    now()
                FROM "Organizations" organization
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Roles" existing
                    WHERE existing."OrganizationId" = organization."Id"
                      AND existing."ScopeType" = 'Project'
                      AND existing."NormalizedName" = 'PROJECT ADMINISTRATOR');
                """);

            migrationBuilder.Sql(
                """
                WITH role_permission_seed("RoleName", "ScopeType", "PermissionKey") AS (
                    VALUES
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'PG.PROJECTS.CREATE'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'PG.PROJECTS.VIEW'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'PG.PROJECTS.UPDATE'),
                        ('ORGANIZATION ADMINISTRATOR', 'Organization', 'PG.PROJECTS.ARCHIVE'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'PG.PROJECTS.VIEW'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'PG.PROJECTS.UPDATE'),
                        ('PROJECT ADMINISTRATOR', 'Project', 'PG.PROJECTS.ARCHIVE')
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAdministratorAssignments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Roles_Id_OrganizationId",
                table: "Roles");

            migrationBuilder.Sql(
                """
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (
                    SELECT "Id"
                    FROM "PermissionDefinitions"
                    WHERE "NormalizedKey" IN (
                        'PG.PROJECTS.CREATE',
                        'PG.PROJECTS.VIEW',
                        'PG.PROJECTS.UPDATE',
                        'PG.PROJECTS.ARCHIVE'));

                DELETE FROM "Roles"
                WHERE "NormalizedName" = 'PROJECT ADMINISTRATOR'
                  AND "ScopeType" = 'Project'
                  AND "RoleType" = 'System';

                DELETE FROM "PermissionDefinitions"
                WHERE "OrganizationId" IS NULL
                  AND "NormalizedKey" IN (
                      'PG.PROJECTS.CREATE',
                      'PG.PROJECTS.VIEW',
                      'PG.PROJECTS.UPDATE',
                      'PG.PROJECTS.ARCHIVE');
                """);
        }
    }
}
