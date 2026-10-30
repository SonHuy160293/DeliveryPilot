using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationInvitations",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TokenVerifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplacesInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentDeliveryMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationInvitations", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationInvitations_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CkOrganizationInvitationsClosure", "(\"Status\" = 'pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL AND \"CloseReason\" IS NULL) OR (\"Status\" IN ('accepted','declined','cancelled','expired') AND \"ClosedAt\" IS NOT NULL AND \"ClosedAt\" >= \"LastIssuedAt\" AND (\"Status\" NOT IN ('accepted','declined') OR \"ClosedByUserId\" IS NOT NULL) AND (\"Status\" <> 'expired' OR \"ClosedByUserId\" IS NULL))");
                    table.CheckConstraint("CkOrganizationInvitationsEmail", "char_length(\"RecipientEmail\") BETWEEN 1 AND 320 AND \"RecipientEmail\" = lower(btrim(\"RecipientEmail\"))");
                    table.CheckConstraint("CkOrganizationInvitationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationInvitationsReplacement", "\"ReplacesInvitationId\" IS NULL OR \"ReplacesInvitationId\" <> \"Id\"");
                    table.CheckConstraint("CkOrganizationInvitationsTimes", "\"ExpiresAt\" > \"LastIssuedAt\" AND \"LastIssuedAt\" >= \"CreatedAt\" AND \"Version\" > 0");
                    table.CheckConstraint("CkOrganizationInvitationsVerifier", "octet_length(\"TokenVerifier\") = 32");
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_OrganizationInvitations_Organizatio~",
                        columns: x => new { x.OrganizationId, x.ReplacesInvitationId },
                        principalSchema: "shared",
                        principalTable: "OrganizationInvitations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationInvitationDeliveries",
                schema: "shared",
                columns: table => new
                {
                    DeliveryMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastEventSequence = table.Column<long>(type: "bigint", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationInvitationDeliveries", x => x.DeliveryMessageId);
                    table.CheckConstraint("CkOrganizationInvitationDeliveriesId", "uuid_extract_version(\"DeliveryMessageId\") = 7");
                    table.CheckConstraint("CkOrganizationInvitationDeliveriesVersions", "\"Version\" > 0 AND \"LastEventSequence\" >= 0 AND \"ExpiresAt\" > \"IssuedAt\"");
                    table.ForeignKey(
                        name: "FK_OrganizationInvitationDeliveries_OrganizationInvitations_Or~",
                        columns: x => new { x.OrganizationId, x.InvitationId },
                        principalSchema: "shared",
                        principalTable: "OrganizationInvitations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationInvitationRoleOffers",
                schema: "shared",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedRoleName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OfferedPermissionSetVersion = table.Column<long>(type: "bigint", nullable: false),
                    OfferedPermissionCodes = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationInvitationRoleOffers", x => new { x.InvitationId, x.RoleId });
                    table.CheckConstraint("CkOrganizationInvitationRoleOffersName", "char_length(btrim(\"OfferedRoleName\")) BETWEEN 1 AND 160 AND \"OfferedRoleName\" = btrim(\"OfferedRoleName\")");
                    table.CheckConstraint("CkOrganizationInvitationRoleOffersSnapshot", "\"OfferedPermissionSetVersion\" > 0 AND jsonb_typeof(\"OfferedPermissionCodes\") = 'array'");
                    table.ForeignKey(
                        name: "FK_OrganizationInvitationRoleOffers_OrganizationInvitations_Or~",
                        columns: x => new { x.OrganizationId, x.InvitationId },
                        principalSchema: "shared",
                        principalTable: "OrganizationInvitations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitationRoleOffers_OrganizationRoles_Organiza~",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "shared",
                        principalTable: "OrganizationRoles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationDeliveries_InvitationId_IssuedAt",
                schema: "shared",
                table: "OrganizationInvitationDeliveries",
                columns: new[] { "InvitationId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationDeliveries_OrganizationId_InvitationId",
                schema: "shared",
                table: "OrganizationInvitationDeliveries",
                columns: new[] { "OrganizationId", "InvitationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationDeliveries_OrganizationId_IssuedAt",
                schema: "shared",
                table: "OrganizationInvitationDeliveries",
                columns: new[] { "OrganizationId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationRoleOffers_OrganizationId_InvitationId",
                schema: "shared",
                table: "OrganizationInvitationRoleOffers",
                columns: new[] { "OrganizationId", "InvitationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationRoleOffers_OrganizationId_RoleId_Invi~",
                schema: "shared",
                table: "OrganizationInvitationRoleOffers",
                columns: new[] { "OrganizationId", "RoleId", "InvitationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_ClosedByUserId",
                schema: "shared",
                table: "OrganizationInvitations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_IssuedByUserId",
                schema: "shared",
                table: "OrganizationInvitations",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_OrganizationId_ReplacesInvitationId",
                schema: "shared",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "ReplacesInvitationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_OrganizationId_Status_ExpiresAt_Id",
                schema: "shared",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "Status", "ExpiresAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UxOrganizationInvitationsPendingRecipient",
                schema: "shared",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "RecipientEmail" },
                unique: true,
                filter: "\"Status\" = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationInvitationDeliveries",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationInvitationRoleOffers",
                schema: "shared");

            migrationBuilder.DropTable(
                name: "OrganizationInvitations",
                schema: "shared");
        }
    }
}
