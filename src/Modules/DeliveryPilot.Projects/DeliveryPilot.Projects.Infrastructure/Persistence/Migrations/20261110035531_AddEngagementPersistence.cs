using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.CreateTable(
                name: "EngagementLinkAttempts",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActingOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagementLinkAttempts", x => x.Id);
                    table.CheckConstraint("CkEngagementLinkAttemptsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementLinkAttemptsIds", "\"UserId\" <> '00000000-0000-0000-0000-000000000000' AND \"ActingOrganizationId\" <> '00000000-0000-0000-0000-000000000000'");
                });

            migrationBuilder.CreateTable(
                name: "Engagements",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientLegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    EngagementName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgreementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AgreementSignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PlannedEffortPersonDays = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientLinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientLinkedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagements", x => x.Id);
                    table.UniqueConstraint("AkEngagementsOrganizationIdId", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CkEngagementsAgreementReference", "char_length(btrim(\"AgreementReference\")) BETWEEN 1 AND 100 AND \"AgreementReference\" = btrim(\"AgreementReference\")");
                    table.CheckConstraint("CkEngagementsAmount", "\"ContractAmount\" IS NULL OR (\"ContractAmount\" >= 0 AND \"ContractAmount\" < 'Infinity'::numeric AND \"ContractAmount\" = trunc(\"ContractAmount\", 2))");
                    table.CheckConstraint("CkEngagementsAmountCurrency", "\"ContractAmount\" IS NULL OR \"ContractCurrency\" IS NOT NULL");
                    table.CheckConstraint("CkEngagementsClientDisplayName", "char_length(btrim(\"ClientDisplayName\")) BETWEEN 1 AND 200 AND \"ClientDisplayName\" = btrim(\"ClientDisplayName\")");
                    table.CheckConstraint("CkEngagementsClientLink", "(\"ClientOrganizationId\" IS NULL AND \"ClientLinkedAt\" IS NULL AND \"ClientLinkedByUserId\" IS NULL) OR (\"ClientOrganizationId\" IS NOT NULL AND \"ClientLinkedAt\" IS NOT NULL AND \"ClientLinkedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkEngagementsClientOrganization", "\"ClientOrganizationId\" IS NULL OR \"ClientOrganizationId\" <> \"OrganizationId\"");
                    table.CheckConstraint("CkEngagementsClosure", "(\"Status\" = 'Active' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" = 'Closed' AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkEngagementsCountry", "\"ClientCountryCode\" IS NULL OR \"ClientCountryCode\" ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("CkEngagementsCurrency", "\"ContractCurrency\" IS NULL OR \"ContractCurrency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CkEngagementsEffort", "\"PlannedEffortPersonDays\" IS NULL OR (\"PlannedEffortPersonDays\" >= 0 AND \"PlannedEffortPersonDays\" < 'Infinity'::numeric AND \"PlannedEffortPersonDays\" = trunc(\"PlannedEffortPersonDays\", 1))");
                    table.CheckConstraint("CkEngagementsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementsIds", "\"OrganizationId\" <> '00000000-0000-0000-0000-000000000000' AND \"CreatedByUserId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CkEngagementsStatus", "\"Status\" IN ('Active', 'Closed')");
                    table.CheckConstraint("CkEngagementsVersion", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "EngagementContacts",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ContactRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagementContacts", x => x.Id);
                    table.UniqueConstraint("AkEngagementContactsOrganizationEngagementId", x => new { x.OrganizationId, x.EngagementId, x.Id });
                    table.CheckConstraint("CkEngagementContactsEmail", "char_length(\"Email\") BETWEEN 3 AND 320 AND \"Email\" = lower(btrim(\"Email\"))");
                    table.CheckConstraint("CkEngagementContactsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementContactsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkEngagementContactsRemoval", "(\"RemovedAt\" IS NULL AND \"RemovedByUserId\" IS NULL) OR (\"RemovedAt\" IS NOT NULL AND \"RemovedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkEngagementContactsRole", "\"ContactRole\" IN ('ClientProductOwner', 'Sponsor', 'Other')");
                    table.CheckConstraint("CkEngagementContactsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkEngagementContactsEngagements",
                        columns: x => new { x.OrganizationId, x.EngagementId },
                        principalSchema: "engagement",
                        principalTable: "Engagements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EngagementEvents",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: false),
                    EngagementVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagementEvents", x => x.Id);
                    table.UniqueConstraint("AkEngagementEventsOrganizationEngagementId", x => new { x.OrganizationId, x.EngagementId, x.Id });
                    table.CheckConstraint("CkEngagementEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
                    table.CheckConstraint("CkEngagementEventsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementEventsSubject", "\"SubjectType\" IN ('Engagement', 'Milestone', 'Contact', 'LinkCode')");
                    table.CheckConstraint("CkEngagementEventsType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
                    table.CheckConstraint("CkEngagementEventsVersion", "\"EngagementVersion\" > 0");
                    table.ForeignKey(
                        name: "FkEngagementEventsEngagements",
                        columns: x => new { x.OrganizationId, x.EngagementId },
                        principalSchema: "engagement",
                        principalTable: "Engagements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EngagementLinkCodes",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeVerifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedForOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagementLinkCodes", x => x.Id);
                    table.UniqueConstraint("AkEngagementLinkCodesOrganizationEngagementId", x => new { x.OrganizationId, x.EngagementId, x.Id });
                    table.CheckConstraint("CkEngagementLinkCodesConsumption", "(\"Status\" = 'Consumed' AND \"ConsumedAt\" IS NOT NULL AND \"ConsumedByUserId\" IS NOT NULL AND \"ConsumedForOrganizationId\" IS NOT NULL) OR (\"Status\" <> 'Consumed' AND \"ConsumedAt\" IS NULL AND \"ConsumedByUserId\" IS NULL AND \"ConsumedForOrganizationId\" IS NULL)");
                    table.CheckConstraint("CkEngagementLinkCodesExpiry", "\"ExpiresAt\" = \"IssuedAt\" + interval '168 hours'");
                    table.CheckConstraint("CkEngagementLinkCodesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementLinkCodesInvalidation", "(\"Status\" = 'Invalidated' AND \"InvalidatedAt\" IS NOT NULL) OR (\"Status\" <> 'Invalidated' AND \"InvalidatedAt\" IS NULL)");
                    table.CheckConstraint("CkEngagementLinkCodesStatus", "\"Status\" IN ('Outstanding', 'Consumed', 'Invalidated')");
                    table.CheckConstraint("CkEngagementLinkCodesVerifier", "octet_length(\"CodeVerifier\") = 32");
                    table.CheckConstraint("CkEngagementLinkCodesVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkEngagementLinkCodesEngagements",
                        columns: x => new { x.OrganizationId, x.EngagementId },
                        principalSchema: "engagement",
                        principalTable: "Engagements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EngagementMilestones",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OriginalTargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentTargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkEngagementMilestones", x => x.Id);
                    table.UniqueConstraint("AkEngagementMilestonesOrganizationEngagementId", x => new { x.OrganizationId, x.EngagementId, x.Id });
                    table.CheckConstraint("CkEngagementMilestonesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkEngagementMilestonesName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkEngagementMilestonesStatus", "\"Status\" IN ('Planned', 'Achieved', 'Missed', 'Retired')");
                    table.CheckConstraint("CkEngagementMilestonesVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkEngagementMilestonesEngagements",
                        columns: x => new { x.OrganizationId, x.EngagementId },
                        principalSchema: "engagement",
                        principalTable: "Engagements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UxEngagementContactsClientProductOwner",
                schema: "engagement",
                table: "EngagementContacts",
                columns: new[] { "OrganizationId", "EngagementId" },
                unique: true,
                filter: "\"ContactRole\" = 'ClientProductOwner' AND \"RemovedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxEngagementEventsOrganizationEngagementOccurredId",
                schema: "engagement",
                table: "EngagementEvents",
                columns: new[] { "OrganizationId", "EngagementId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IxEngagementLinkAttemptsUserAttemptedAt",
                schema: "engagement",
                table: "EngagementLinkAttempts",
                columns: new[] { "UserId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "UxEngagementLinkCodesOutstanding",
                schema: "engagement",
                table: "EngagementLinkCodes",
                columns: new[] { "OrganizationId", "EngagementId" },
                unique: true,
                filter: "\"Status\" = 'Outstanding'");

            migrationBuilder.CreateIndex(
                name: "UxEngagementLinkCodesVerifier",
                schema: "engagement",
                table: "EngagementLinkCodes",
                column: "CodeVerifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IxEngagementsClientOrganization",
                schema: "engagement",
                table: "Engagements",
                column: "ClientOrganizationId",
                filter: "\"ClientOrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IxEngagementsOrganizationStatusCreatedId",
                schema: "engagement",
                table: "Engagements",
                columns: new[] { "OrganizationId", "Status", "CreatedAt", "Id" });

            // EF does not model expression indexes; duplicates intentionally remain permitted.
            migrationBuilder.Sql("""
                CREATE INDEX "IxEngagementsActiveAgreementReferenceCi"
                ON engagement."Engagements" ("OrganizationId", lower("AgreementReference"))
                WHERE "Status" = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EngagementContacts",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "EngagementEvents",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "EngagementLinkAttempts",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "EngagementLinkCodes",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "EngagementMilestones",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "Engagements",
                schema: "engagement");
        }
    }
}
