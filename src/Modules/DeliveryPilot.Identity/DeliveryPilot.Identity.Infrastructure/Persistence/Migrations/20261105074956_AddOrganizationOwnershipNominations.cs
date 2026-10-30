using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationOwnershipNominations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationOwnershipNominations",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    NominatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationOwnershipNominations", x => x.Id);
                    table.CheckConstraint("CkOrganizationOwnershipNominationsClosure", "(\"Status\" = 'pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL AND \"Reason\" IS NULL AND \"Version\" = 1 AND \"UpdatedAt\" = \"CreatedAt\") OR (\"Status\" IN ('accepted','declined','cancelled','expired') AND \"ClosedAt\" IS NOT NULL AND \"ClosedAt\" >= \"CreatedAt\" AND \"UpdatedAt\" = \"ClosedAt\" AND \"Version\" = 2 AND ((\"Status\" = 'expired' AND \"ClosedByUserId\" IS NULL AND \"ClosedAt\" >= \"ExpiresAt\") OR (\"Status\" <> 'expired' AND \"ClosedByUserId\" IS NOT NULL AND \"ClosedAt\" < \"ExpiresAt\")))");
                    table.CheckConstraint("CkOrganizationOwnershipNominationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkOrganizationOwnershipNominationsReason", "\"Reason\" IS NULL OR (char_length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Status\" = 'cancelled')");
                    table.CheckConstraint("CkOrganizationOwnershipNominationsTimes", "\"ExpiresAt\" = \"CreatedAt\" + interval '168 hours' AND \"UpdatedAt\" >= \"CreatedAt\" AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_OrganizationOwnershipNominations_OrganizationMemberships_Or~",
                        columns: x => new { x.OrganizationId, x.MembershipId },
                        principalSchema: "shared",
                        principalTable: "OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationOwnershipNominations_Organizations_Organization~",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationOwnershipNominations_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationOwnershipNominations_Users_NominatedByUserId",
                        column: x => x.NominatedByUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationOwnershipNominations_ClosedByUserId",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationOwnershipNominations_MembershipId_Status_Create~",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                columns: new[] { "MembershipId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationOwnershipNominations_NominatedByUserId",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                column: "NominatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationOwnershipNominations_OrganizationId_Status_Crea~",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                columns: new[] { "OrganizationId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationOwnershipNominations_Status_ExpiresAt",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UxOwnershipNominationsPendingMember",
                schema: "shared",
                table: "OrganizationOwnershipNominations",
                columns: new[] { "OrganizationId", "MembershipId" },
                unique: true,
                filter: "\"Status\" = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationOwnershipNominations",
                schema: "shared");
        }
    }
}
