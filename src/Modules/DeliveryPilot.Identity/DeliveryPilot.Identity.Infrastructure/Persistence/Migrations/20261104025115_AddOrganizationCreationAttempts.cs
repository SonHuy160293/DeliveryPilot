using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationCreationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationCreationAttempts",
                schema: "shared",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReplayUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseSchemaVersion = table.Column<short>(type: "smallint", nullable: false),
                    ResultSnapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkOrganizationCreationAttempts", x => new { x.UserId, x.AttemptId });
                    table.CheckConstraint("CkOrganizationCreationAttemptsDeadline", "\"ReplayUntil\" = \"CreatedAt\" + interval '24 hours'");
                    table.CheckConstraint("CkOrganizationCreationAttemptsFingerprint", "octet_length(\"RequestFingerprint\") = 32");
                    table.CheckConstraint("CkOrganizationCreationAttemptsIds", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AttemptId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OrganizationId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CkOrganizationCreationAttemptsSnapshot", "(\"ResponseSchemaVersion\" = 1 AND jsonb_typeof(\"ResultSnapshot\") = 'object' AND octet_length(\"ResultSnapshot\"::text) <= 8192 AND \"ResultSnapshot\" ?& ARRAY['organizationId','name','status','membershipId','roles','version','membershipVersion','resultKind','snapshotAt'] AND \"ResultSnapshot\" - ARRAY['organizationId','name','status','membershipId','roles','version','membershipVersion','resultKind','snapshotAt'] = '{}'::jsonb AND \"ResultSnapshot\"->>'organizationId' = \"OrganizationId\"::text AND jsonb_typeof(\"ResultSnapshot\"->'membershipId') = 'string' AND jsonb_typeof(\"ResultSnapshot\"->'name') = 'string' AND jsonb_typeof(\"ResultSnapshot\"->'snapshotAt') = 'string' AND \"ResultSnapshot\"->>'status' = 'active' AND \"ResultSnapshot\"->>'resultKind' = 'creationSnapshot' AND \"ResultSnapshot\"->'roles' = '[\"Owner\"]'::jsonb AND \"ResultSnapshot\"->'version' = '1'::jsonb AND \"ResultSnapshot\"->'membershipVersion' = '1'::jsonb) IS TRUE");
                    table.ForeignKey(
                        name: "FK_OrganizationCreationAttempts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationCreationAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationCreationAttempts_OrganizationId",
                schema: "shared",
                table: "OrganizationCreationAttempts",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationCreationAttempts",
                schema: "shared");
        }
    }
}
