using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionGraph.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class M02_AddOrganizationsMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OldValuesJson = table.Column<string>(type: "text", nullable: true),
                    NewValuesJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuspendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuthorizationVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermissionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Module = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PermissionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllowedScopes = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsRequestable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RoleType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRequestable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_PermissionDefinitions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "PermissionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_OccurredAtUtc_Id",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetType_TargetId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "TargetType", "TargetId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_UserId",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_UserId_Status",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_UserId_Status",
                table: "OrganizationMemberships",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_NormalizedName",
                table: "Organizations",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_OwnerUserId",
                table: "Organizations",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_NormalizedKey",
                table: "PermissionDefinitions",
                column: "NormalizedKey",
                unique: true,
                filter: "\"OrganizationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDefinitions_OrganizationId_NormalizedKey_IsActive",
                table: "PermissionDefinitions",
                columns: new[] { "OrganizationId", "NormalizedKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId_RoleId",
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_OrganizationId_ScopeType_IsActive",
                table: "Roles",
                columns: new[] { "OrganizationId", "ScopeType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_OrganizationId_ScopeType_NormalizedName",
                table: "Roles",
                columns: new[] { "OrganizationId", "ScopeType", "NormalizedName" },
                unique: true,
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "OrganizationMemberships");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "PermissionDefinitions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
