using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "project");

            migrationBuilder.CreateTable(
                name: "ProjectBriefs",
                schema: "project",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Problem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ObjectivesAndSuccessCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ScopeAndExclusions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Assumptions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConstraintsAndDependencies = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OpenQuestions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProductGoal = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectBriefs", x => x.ProjectId);
                    table.CheckConstraint("CkProjectBriefsUpdate", "(\"UpdatedAt\" IS NULL AND \"UpdatedByUserId\" IS NULL) OR (\"UpdatedAt\" IS NOT NULL AND \"UpdatedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectBriefsVersion", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProjectEvents",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProjectVersion = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectEvents", x => x.Id);
                    table.UniqueConstraint("AkProjectEventsOrganizationProjectId", x => new { x.OrganizationId, x.ProjectId, x.Id });
                    table.CheckConstraint("CkProjectEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
                    table.CheckConstraint("CkProjectEventsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectEventsSubject", "\"SubjectType\" IN ('Project', 'Brief', 'Participant', 'RoleGrant', 'Invitation', 'Stakeholder', 'ClientPo', 'GuestShare')");
                    table.CheckConstraint("CkProjectEventsType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
                    table.CheckConstraint("CkProjectEventsVersion", "\"ProjectVersion\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProjectGuestInvitationDeliveries",
                schema: "project",
                columns: table => new
                {
                    DeliveryMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastEventSequence = table.Column<long>(type: "bigint", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectGuestInvitationDeliveries", x => x.DeliveryMessageId);
                    table.CheckConstraint("CkProjectGuestInvitationDeliveriesExpiry", "\"ExpiresAt\" > \"IssuedAt\"");
                    table.CheckConstraint("CkProjectGuestInvitationDeliveriesId", "uuid_extract_version(\"DeliveryMessageId\") = 7");
                    table.CheckConstraint("CkProjectGuestInvitationDeliveriesSequence", "\"LastEventSequence\" >= 0");
                    table.CheckConstraint("CkProjectGuestInvitationDeliveriesStatus", "\"Status\" IN ('queued', 'retrying', 'provider-accepted', 'delivered', 'bounced', 'complained', 'failed', 'suppressed', 'expired', 'obsolete')");
                    table.CheckConstraint("CkProjectGuestInvitationDeliveriesVersion", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProjectGuestInvitations",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    OfferedRoleCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenVerifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentDeliveryMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacesInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectGuestInvitations", x => x.Id);
                    table.UniqueConstraint("AkProjectGuestInvitationsOrganizationProjectId", x => new { x.OrganizationId, x.ProjectId, x.Id });
                    table.CheckConstraint("CkProjectGuestInvitationsClosure", "(\"Status\" = 'Pending' AND \"ClosedAt\" IS NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" = 'Expired' AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NULL) OR (\"Status\" IN ('Accepted', 'Declined', 'Cancelled') AND \"ClosedAt\" IS NOT NULL AND \"ClosedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectGuestInvitationsEmail", "char_length(\"RecipientEmail\") BETWEEN 3 AND 320 AND \"RecipientEmail\" = lower(btrim(\"RecipientEmail\"))");
                    table.CheckConstraint("CkProjectGuestInvitationsExpiry", "\"ExpiresAt\" = \"LastIssuedAt\" + interval '168 hours'");
                    table.CheckConstraint("CkProjectGuestInvitationsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectGuestInvitationsRole", "\"OfferedRoleCode\" IN ('ProjectManager', 'BusinessAnalyst', 'Developer', 'QualityAssurance', 'DevOps', 'Viewer')");
                    table.CheckConstraint("CkProjectGuestInvitationsStatus", "\"Status\" IN ('Pending', 'Accepted', 'Declined', 'Cancelled', 'Expired')");
                    table.CheckConstraint("CkProjectGuestInvitationsVerifier", "octet_length(\"TokenVerifier\") = 32");
                    table.CheckConstraint("CkProjectGuestInvitationsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkProjectGuestInvitationsReplacedInvitation",
                        columns: x => new { x.OrganizationId, x.ProjectId, x.ReplacesInvitationId },
                        principalSchema: "project",
                        principalTable: "ProjectGuestInvitations",
                        principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectParticipantRoles",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectParticipantRoles", x => x.Id);
                    table.UniqueConstraint("AkProjectParticipantRolesOrganizationProjectId", x => new { x.OrganizationId, x.ProjectId, x.Id });
                    table.CheckConstraint("CkProjectParticipantRolesCode", "\"RoleCode\" IN ('ProjectAdministrator', 'ProjectManager', 'BusinessAnalyst', 'Developer', 'QualityAssurance', 'DevOps', 'Viewer')");
                    table.CheckConstraint("CkProjectParticipantRolesId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectParticipantRolesRevocation", "(\"RevokedAt\" IS NULL AND \"RevokedByUserId\" IS NULL) OR (\"RevokedAt\" IS NOT NULL AND \"RevokedByUserId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ProjectParticipants",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstJoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActiveSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmittedViaInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectParticipants", x => x.Id);
                    table.UniqueConstraint("AkProjectParticipantsOrganizationProjectId", x => new { x.OrganizationId, x.ProjectId, x.Id });
                    table.CheckConstraint("CkProjectParticipantsEnd", "(\"Status\" = 'Active' AND \"EndedAt\" IS NULL AND \"EndedByUserId\" IS NULL) OR (\"Status\" <> 'Active' AND \"EndedAt\" IS NOT NULL AND \"EndedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectParticipantsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectParticipantsKind", "\"Kind\" IN ('Member', 'Guest')");
                    table.CheckConstraint("CkProjectParticipantsStatus", "\"Status\" IN ('Active', 'Removed', 'Revoked', 'Left')");
                    table.CheckConstraint("CkProjectParticipantsUser", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CkProjectParticipantsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkProjectParticipantsInvitations",
                        columns: x => new { x.OrganizationId, x.ProjectId, x.AdmittedViaInvitationId },
                        principalSchema: "project",
                        principalTable: "ProjectGuestInvitations",
                        principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SprintLengthWeeks = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    ClientProductOwnerParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientPoAssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientPoAssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientPoWaiverReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjects", x => x.Id);
                    table.UniqueConstraint("AkProjectsOrganizationId", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CkProjectsActivation", "(\"Status\" = 'Draft' AND \"ActivatedAt\" IS NULL AND \"ActivatedByUserId\" IS NULL) OR (\"Status\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"ActivatedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectsClientPo", "(\"ClientProductOwnerParticipantId\" IS NULL AND \"ClientPoAssignedAt\" IS NULL AND \"ClientPoAssignedByUserId\" IS NULL) OR (\"ClientProductOwnerParticipantId\" IS NOT NULL AND \"ClientPoAssignedAt\" IS NOT NULL AND \"ClientPoAssignedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectsEngagement", "\"Kind\" = 'Client' OR \"EngagementId\" IS NULL");
                    table.CheckConstraint("CkProjectsId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectsIds", "\"OrganizationId\" <> '00000000-0000-0000-0000-000000000000' AND \"CreatedByUserId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CkProjectsKind", "\"Kind\" IN ('Client', 'Internal')");
                    table.CheckConstraint("CkProjectsName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 160 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkProjectsPurpose", "char_length(btrim(\"Purpose\")) BETWEEN 1 AND 500 AND \"Purpose\" = btrim(\"Purpose\")");
                    table.CheckConstraint("CkProjectsSprintLength", "\"SprintLengthWeeks\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CkProjectsStatus", "\"Status\" IN ('Draft', 'Active')");
                    table.CheckConstraint("CkProjectsVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkProjectsClientProductOwner",
                        columns: x => new { x.OrganizationId, x.Id, x.ClientProductOwnerParticipantId },
                        principalSchema: "project",
                        principalTable: "ProjectParticipants",
                        principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FkProjectsEngagements",
                        columns: x => new { x.OrganizationId, x.EngagementId },
                        principalSchema: "engagement",
                        principalTable: "Engagements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStakeholders",
                schema: "project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Responsibilities = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecisionAreas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PkProjectStakeholders", x => x.Id);
                    table.UniqueConstraint("AkProjectStakeholdersOrganizationProjectId", x => new { x.OrganizationId, x.ProjectId, x.Id });
                    table.CheckConstraint("CkProjectStakeholdersEmail", "\"Email\" IS NULL OR (char_length(\"Email\") BETWEEN 3 AND 320 AND \"Email\" = lower(btrim(\"Email\")))");
                    table.CheckConstraint("CkProjectStakeholdersId", "uuid_extract_version(\"Id\") = 7");
                    table.CheckConstraint("CkProjectStakeholdersName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CkProjectStakeholdersRetirement", "(\"RetiredAt\" IS NULL AND \"RetiredByUserId\" IS NULL) OR (\"RetiredAt\" IS NOT NULL AND \"RetiredByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CkProjectStakeholdersSide", "\"Side\" IN ('Vendor', 'Client')");
                    table.CheckConstraint("CkProjectStakeholdersVersion", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FkProjectStakeholdersParticipants",
                        columns: x => new { x.OrganizationId, x.ProjectId, x.ParticipantId },
                        principalSchema: "project",
                        principalTable: "ProjectParticipants",
                        principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FkProjectStakeholdersProjects",
                        columns: x => new { x.OrganizationId, x.ProjectId },
                        principalSchema: "project",
                        principalTable: "Projects",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UxProjectBriefsOrganizationProject",
                schema: "project",
                table: "ProjectBriefs",
                columns: new[] { "OrganizationId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IxProjectEventsOrganizationProjectOccurredId",
                schema: "project",
                table: "ProjectEvents",
                columns: new[] { "OrganizationId", "ProjectId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IxProjectGuestInvitationDeliveriesInvitation",
                schema: "project",
                table: "ProjectGuestInvitationDeliveries",
                columns: new[] { "OrganizationId", "ProjectId", "InvitationId" });

            migrationBuilder.CreateIndex(
                name: "IxProjectGuestInvitationDeliveriesInvitationIssued",
                schema: "project",
                table: "ProjectGuestInvitationDeliveries",
                columns: new[] { "InvitationId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IxProjectGuestInvitationDeliveriesProjectIssued",
                schema: "project",
                table: "ProjectGuestInvitationDeliveries",
                columns: new[] { "OrganizationId", "ProjectId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IxProjectGuestInvitationsReplacement",
                schema: "project",
                table: "ProjectGuestInvitations",
                columns: new[] { "OrganizationId", "ProjectId", "ReplacesInvitationId" });

            migrationBuilder.CreateIndex(
                name: "UxProjectGuestInvitationsPending",
                schema: "project",
                table: "ProjectGuestInvitations",
                columns: new[] { "OrganizationId", "ProjectId", "RecipientEmail" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IxProjectParticipantRolesActiveProjectRole",
                schema: "project",
                table: "ProjectParticipantRoles",
                columns: new[] { "OrganizationId", "ProjectId", "RoleCode" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxProjectParticipantRolesParticipant",
                schema: "project",
                table: "ProjectParticipantRoles",
                columns: new[] { "OrganizationId", "ProjectId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "UxProjectParticipantRolesActive",
                schema: "project",
                table: "ProjectParticipantRoles",
                columns: new[] { "OrganizationId", "ParticipantId", "RoleCode" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IxProjectParticipantsInvitation",
                schema: "project",
                table: "ProjectParticipants",
                columns: new[] { "OrganizationId", "ProjectId", "AdmittedViaInvitationId" });

            migrationBuilder.CreateIndex(
                name: "IxProjectParticipantsOrganizationUserStatus",
                schema: "project",
                table: "ProjectParticipants",
                columns: new[] { "OrganizationId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IxProjectParticipantsUserStatus",
                schema: "project",
                table: "ProjectParticipants",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UxProjectParticipantsUser",
                schema: "project",
                table: "ProjectParticipants",
                columns: new[] { "OrganizationId", "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IxProjectsClientProductOwner",
                schema: "project",
                table: "Projects",
                columns: new[] { "OrganizationId", "Id", "ClientProductOwnerParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IxProjectsOrganizationEngagement",
                schema: "project",
                table: "Projects",
                columns: new[] { "OrganizationId", "EngagementId" });

            migrationBuilder.CreateIndex(
                name: "IxProjectsOrganizationStatusCreatedId",
                schema: "project",
                table: "Projects",
                columns: new[] { "OrganizationId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IxProjectStakeholdersParticipant",
                schema: "project",
                table: "ProjectStakeholders",
                columns: new[] { "OrganizationId", "ProjectId", "ParticipantId" });

            migrationBuilder.AddForeignKey(
                name: "FkProjectBriefsProjects",
                schema: "project",
                table: "ProjectBriefs",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectEventsProjects",
                schema: "project",
                table: "ProjectEvents",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectGuestInvitationDeliveriesInvitations",
                schema: "project",
                table: "ProjectGuestInvitationDeliveries",
                columns: new[] { "OrganizationId", "ProjectId", "InvitationId" },
                principalSchema: "project",
                principalTable: "ProjectGuestInvitations",
                principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectGuestInvitationDeliveriesProjects",
                schema: "project",
                table: "ProjectGuestInvitationDeliveries",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectGuestInvitationsProjects",
                schema: "project",
                table: "ProjectGuestInvitations",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectParticipantRolesParticipants",
                schema: "project",
                table: "ProjectParticipantRoles",
                columns: new[] { "OrganizationId", "ProjectId", "ParticipantId" },
                principalSchema: "project",
                principalTable: "ProjectParticipants",
                principalColumns: new[] { "OrganizationId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectParticipantRolesProjects",
                schema: "project",
                table: "ProjectParticipantRoles",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FkProjectParticipantsProjects",
                schema: "project",
                table: "ProjectParticipants",
                columns: new[] { "OrganizationId", "ProjectId" },
                principalSchema: "project",
                principalTable: "Projects",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FkProjectGuestInvitationsProjects",
                schema: "project",
                table: "ProjectGuestInvitations");

            migrationBuilder.DropForeignKey(
                name: "FkProjectParticipantsProjects",
                schema: "project",
                table: "ProjectParticipants");

            migrationBuilder.DropTable(
                name: "ProjectBriefs",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectEvents",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectGuestInvitationDeliveries",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectParticipantRoles",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectStakeholders",
                schema: "project");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectParticipants",
                schema: "project");

            migrationBuilder.DropTable(
                name: "ProjectGuestInvitations",
                schema: "project");
        }
    }
}
