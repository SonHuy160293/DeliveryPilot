using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityAuditEvents",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipVersion = table.Column<long>(type: "bigint", nullable: true),
                    AggregateType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateVersion = table.Column<long>(type: "bigint", nullable: false),
                    EventOrdinal = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    DetailsSchemaVersion = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    Details = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkIdentityAuditEvents", x => x.Id);
                    table.CheckConstraint("CkIdentityAuditEventsActor", "(\"ActorKind\" = 'user' AND \"ActorUserId\" IS NOT NULL AND \"ActorReference\" IS NULL) OR (\"ActorKind\" IN ('system', 'operator') AND \"ActorUserId\" IS NULL AND \"ActorReference\" IS NOT NULL AND char_length(btrim(\"ActorReference\")) BETWEEN 1 AND 200 AND \"ActorReference\" = btrim(\"ActorReference\"))");
                    table.CheckConstraint("CkIdentityAuditEventsActorKind", "\"ActorKind\" IN ('user', 'system', 'operator')");
                    table.CheckConstraint("CkIdentityAuditEventsAggregateType", "char_length(btrim(\"AggregateType\")) BETWEEN 1 AND 80 AND \"AggregateType\" = btrim(\"AggregateType\")");
                    table.CheckConstraint("CkIdentityAuditEventsCorrelationId", "char_length(btrim(\"CorrelationId\")) BETWEEN 1 AND 100 AND \"CorrelationId\" = btrim(\"CorrelationId\")");
                    table.CheckConstraint("CkIdentityAuditEventsDetailsObject", "jsonb_typeof(\"Details\") = 'object'");
                    table.CheckConstraint("CkIdentityAuditEventsEventType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
                    table.CheckConstraint("CkIdentityAuditEventsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkIdentityAuditEventsMembership", "(\"MembershipId\" IS NULL AND \"MembershipVersion\" IS NULL) OR (\"OrganizationId\" IS NOT NULL AND \"MembershipId\" IS NOT NULL AND \"MembershipVersion\" IS NOT NULL AND \"MembershipVersion\" > 0)");
                    table.CheckConstraint("CkIdentityAuditEventsOutcome", "\"Outcome\" IN ('succeeded', 'denied', 'failed')");
                    table.CheckConstraint("CkIdentityAuditEventsReason", "\"Reason\" IS NULL OR (char_length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\"))");
                    table.CheckConstraint("CkIdentityAuditEventsVersions", "\"AggregateVersion\" > 0 AND \"EventOrdinal\" > 0 AND \"DetailsSchemaVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_IdentityAuditEvents_OrganizationMemberships_OrganizationId_~",
                        columns: x => new { x.OrganizationId, x.MembershipId },
                        principalSchema: "shared",
                        principalTable: "OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentityAuditEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "shared",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentityAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "shared",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAuditEvents_OrganizationId_MembershipId",
                schema: "shared",
                table: "IdentityAuditEvents",
                columns: new[] { "OrganizationId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IxIdentityAuditEventsActorOccurredAtId",
                schema: "shared",
                table: "IdentityAuditEvents",
                columns: new[] { "ActorUserId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IxIdentityAuditEventsOrganizationOccurredAtId",
                schema: "shared",
                table: "IdentityAuditEvents",
                columns: new[] { "OrganizationId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UxIdentityAuditEventsAggregateRevisionOrdinal",
                schema: "shared",
                table: "IdentityAuditEvents",
                columns: new[] { "AggregateType", "AggregateId", "AggregateVersion", "EventOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UxIdentityAuditEventsMembershipRevisionOrdinal",
                schema: "shared",
                table: "IdentityAuditEvents",
                columns: new[] { "MembershipId", "MembershipVersion", "EventOrdinal" },
                unique: true,
                filter: "\"MembershipId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityAuditEvents",
                schema: "shared");
        }
    }
}
