#nullable disable

namespace PermissionGraph.Infrastructure.Data.Migrations
{
    public partial class M07_AddRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                    table.CheckConstraint("CK_RoleAssignments_ExpirationAfterStart", "\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"StartsAtUtc\"");
                    table.CheckConstraint("CK_RoleAssignments_OrganizationScopeId", "\"ScopeType\" <> 'Organization' OR \"ScopeId\" = \"OrganizationId\"");
                    table.CheckConstraint("CK_RoleAssignments_ScopeType", "\"ScopeType\" IN ('Organization', 'Project')");
                    table.CheckConstraint("CK_RoleAssignments_Status", "\"Status\" IN ('Scheduled', 'Active', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "FK_RoleAssignments_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_AspNetUsers_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Roles_RoleId_OrganizationId",
                        columns: x => new { x.RoleId, x.OrganizationId },
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_ExpiresAtUtc_Status",
                table: "RoleAssignments",
                columns: new[] { "ExpiresAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_GrantedByUserId",
                table: "RoleAssignments",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_OrganizationId_Status",
                table: "RoleAssignments",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_OrganizationId_UserId_ScopeType_ScopeId_Sta~",
                table: "RoleAssignments",
                columns: new[] { "OrganizationId", "UserId", "ScopeType", "ScopeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_RevokedByUserId",
                table: "RoleAssignments",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_RoleId_OrganizationId",
                table: "RoleAssignments",
                columns: new[] { "RoleId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId_RoleId_ScopeType_ScopeId",
                table: "RoleAssignments",
                columns: new[] { "UserId", "RoleId", "ScopeType", "ScopeId" },
                unique: true,
                filter: "\"Status\" IN ('Scheduled', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId_StartsAtUtc_ExpiresAtUtc_Status",
                table: "RoleAssignments",
                columns: new[] { "UserId", "StartsAtUtc", "ExpiresAtUtc", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RoleAssignments");
        }
    }
}
