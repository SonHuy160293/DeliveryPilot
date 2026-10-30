using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatusChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizations", x => x.Id);
                    table.CheckConstraint("CkOrganizationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkOrganizationsStatus", "\"Status\" IN ('active', 'suspended', 'archived')");
                    table.CheckConstraint("CkOrganizationsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_Organizations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Organizations_Users_StatusChangedByUserId",
                        column: x => x.StatusChangedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMemberships",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatusChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationMemberships", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationMemberships_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CkOrganizationMembershipsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationMembershipsStatus", "\"Status\" IN ('active', 'suspended', 'removed')");
                    table.CheckConstraint("CkOrganizationMembershipsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_Users_StatusChangedByUserId",
                        column: x => x.StatusChangedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationRoles",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: true),
                    PermissionSetVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationRoles", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationRoles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CkOrganizationRolesCode", "char_length(btrim(\"Code\")) BETWEEN 1 AND 80 AND \"Code\" = btrim(\"Code\")");
                    table.CheckConstraint("CkOrganizationRolesDescription", "\"Description\" IS NULL OR char_length(\"Description\") <= 1000");
                    table.CheckConstraint("CkOrganizationRolesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationRolesName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkOrganizationRolesOrigin", "(\"Origin\" = 'system' AND \"TemplateVersion\" IS NOT NULL AND \"TemplateVersion\" > 0) OR (\"Origin\" = 'custom' AND \"TemplateVersion\" IS NULL AND \"CreatedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkOrganizationRolesVersions", "\"Version\" > 0 AND \"PermissionSetVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_OrganizationRoles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRoles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationRoleAssignments",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantReason = table.Column<string>(type: "text", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationRoleAssignments", x => x.Id);
                    table.CheckConstraint("CkOrganizationRoleAssignmentsExpiry", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"GrantedAt\"");
                    table.CheckConstraint("CkOrganizationRoleAssignmentsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationRoleAssignmentsRevocation", "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"GrantedAt\"");
                    table.CheckConstraint("CkOrganizationRoleAssignmentsVersions", "\"Version\" > 0 AND \"GrantVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_OrganizationRoleAssignments_OrganizationMemberships_Organiz~",
                        columns: x => new { x.OrganizationId, x.MembershipId },
                        principalSchema: "shared",
                        principalTable: "OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRoleAssignments_OrganizationRoles_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "shared",
                        principalTable: "OrganizationRoles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRoleAssignments_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRoleAssignments_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationRolePermissions",
                schema: "shared",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationRolePermissions", x => new { x.RoleId, x.PermissionCode });
                    table.CheckConstraint("CkOrganizationRolePermissionsCode", "char_length(btrim(\"PermissionCode\")) BETWEEN 1 AND 160 AND \"PermissionCode\" = btrim(\"PermissionCode\")");
                    table.ForeignKey(
                        name: "FK_OrganizationRolePermissions_OrganizationRoles_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "shared",
                        principalTable: "OrganizationRoles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_Status_Id",
                schema: "shared",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_StatusChangedByUserId",
                schema: "shared",
                table: "OrganizationMemberships",
                column: "StatusChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_UserId_Status_Id",
                schema: "shared",
                table: "OrganizationMemberships",
                columns: new[] { "UserId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "UxOrganizationMembershipsOrganizationUser",
                schema: "shared",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoleAssignments_GrantedByUserId",
                schema: "shared",
                table: "OrganizationRoleAssignments",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoleAssignments_OrganizationId_MembershipId_Rol~",
                schema: "shared",
                table: "OrganizationRoleAssignments",
                columns: new[] { "OrganizationId", "MembershipId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoleAssignments_OrganizationId_RoleId_Membershi~",
                schema: "shared",
                table: "OrganizationRoleAssignments",
                columns: new[] { "OrganizationId", "RoleId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoleAssignments_RevokedByUserId",
                schema: "shared",
                table: "OrganizationRoleAssignments",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRolePermissions_OrganizationId_RoleId",
                schema: "shared",
                table: "OrganizationRolePermissions",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoles_CreatedByUserId",
                schema: "shared",
                table: "OrganizationRoles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CreatedByUserId",
                schema: "shared",
                table: "Organizations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_StatusChangedByUserId",
                schema: "shared",
                table: "Organizations",
                column: "StatusChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IxOrganizationsStatusId",
                schema: "shared",
                table: "Organizations",
                columns: new[] { "Status", "Id" });

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"UxOrganizationRolesOrganizationCodeCi\" ON shared.\"OrganizationRoles\" (\"OrganizationId\", lower(\"Code\"));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"UxOrganizationRolesOrganizationNameCi\" ON shared.\"OrganizationRoles\" (\"OrganizationId\", lower(\"Name\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationRoleAssignments",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationRolePermissions",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationMemberships",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationRoles",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "shared");
        }
    }
}
