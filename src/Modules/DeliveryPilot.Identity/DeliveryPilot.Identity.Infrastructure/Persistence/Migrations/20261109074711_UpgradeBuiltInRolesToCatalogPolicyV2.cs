using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-DP-003-020 / ADR-001 D-03: grants the catalog policy version 2 workspace entry-point codes to the
    /// built-in Owner and Admin roles of existing organizations. Each upgraded role advances its permission-set
    /// version (so pending invitation offers of that role must be reissued) and records one system-attributed
    /// <c>organization.role.permissions-changed</c> audit event. Retired and custom roles are not changed.
    /// </summary>
    public partial class UpgradeBuiltInRolesToCatalogPolicyV2 : Migration
    {
        private const string UpgradeReference = "catalog-policy-v2-upgrade";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "CatalogPolicyV2Targets" ON COMMIT DROP AS
                SELECT role."Id" AS "RoleId",
                       role."OrganizationId",
                       role."Version" AS "PreviousVersion",
                       role."PermissionSetVersion" AS "PreviousPermissionSetVersion",
                       COALESCE(
                           (SELECT array_agg(permission."PermissionCode" ORDER BY permission."PermissionCode" COLLATE "C")
                            FROM shared."OrganizationRolePermissions" permission
                            WHERE permission."RoleId" = role."Id"),
                           ARRAY[]::text[]) AS "PreviousCodes"
                FROM shared."OrganizationRoles" role
                WHERE role."Origin" = 'system'
                  AND role."Code" IN ('owner', 'admin')
                  AND role."RetiredAt" IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM shared."OrganizationRolePermissions" existing
                      WHERE existing."RoleId" = role."Id"
                        AND existing."PermissionCode" IN ('organization.engagements.create', 'organization.engagements.read-all', 'organization.projects.create'));

                INSERT INTO shared."OrganizationRolePermissions" ("OrganizationId", "RoleId", "PermissionCode", "CatalogPolicyVersion")
                SELECT target."OrganizationId", target."RoleId", code.value, 2
                FROM "CatalogPolicyV2Targets" target
                CROSS JOIN (VALUES ('organization.engagements.create'), ('organization.engagements.read-all'), ('organization.projects.create')) AS code(value)
                ON CONFLICT DO NOTHING;

                UPDATE shared."OrganizationRoles" role
                SET "Version" = target."PreviousVersion" + 1,
                    "PermissionSetVersion" = target."PreviousPermissionSetVersion" + 1,
                    "TemplateVersion" = 2,
                    "UpdatedAt" = now()
                FROM "CatalogPolicyV2Targets" target
                WHERE role."Id" = target."RoleId";

                INSERT INTO shared."IdentityAuditEvents" (
                    "Id", "OrganizationId", "MembershipId", "MembershipVersion", "AggregateType", "AggregateId",
                    "AggregateVersion", "EventOrdinal", "EventType", "Outcome", "ActorKind", "ActorUserId",
                    "ActorReference", "OccurredAt", "RecordedAt", "CorrelationId", "Reason", "DetailsSchemaVersion", "Details")
                SELECT uuidv7(), target."OrganizationId", NULL, NULL, 'OrganizationRole', target."RoleId",
                       target."PreviousVersion" + 1, 1, 'organization.role.permissions-changed', 'succeeded', 'system', NULL,
                       '{UpgradeReference}', now(), now(), '{UpgradeReference}', NULL, 1,
                       jsonb_build_object(
                           'origin', 'system',
                           'templateVersion', 2,
                           'previousPermissionSetVersion', target."PreviousPermissionSetVersion",
                           'permissionSetVersion', target."PreviousPermissionSetVersion" + 1,
                           'addedPermissionCodes', jsonb_build_array('organization.engagements.create', 'organization.engagements.read-all', 'organization.projects.create'),
                           'removedPermissionCodes', '[]'::jsonb,
                           'permissionCodes', (
                               SELECT to_jsonb(array_agg(code ORDER BY code COLLATE "C"))
                               FROM unnest(target."PreviousCodes" || ARRAY['organization.engagements.create', 'organization.engagements.read-all', 'organization.projects.create']) AS code))
                FROM "CatalogPolicyV2Targets" target;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback: removes the upgraded grants and the upgrade audit events, restores the previous
            // permission-set and template versions, and advances Version so optimistic concurrency stays monotonic.
            // Grants of the new codes made to custom roles after the upgrade are left in place; the version 1
            // catalog does not recognize them, so they stay ineffective.
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "CatalogPolicyV2Rollback" ON COMMIT DROP AS
                SELECT audit."Id" AS "AuditId",
                       audit."AggregateId" AS "RoleId",
                       (audit."Details" ->> 'previousPermissionSetVersion')::bigint AS "PreviousPermissionSetVersion"
                FROM shared."IdentityAuditEvents" audit
                WHERE audit."EventType" = 'organization.role.permissions-changed'
                  AND audit."ActorKind" = 'system'
                  AND audit."ActorReference" = '{UpgradeReference}';

                DELETE FROM shared."OrganizationRolePermissions" permission
                USING "CatalogPolicyV2Rollback" rollback
                WHERE permission."RoleId" = rollback."RoleId"
                  AND permission."PermissionCode" IN ('organization.engagements.create', 'organization.engagements.read-all', 'organization.projects.create');

                UPDATE shared."OrganizationRoles" role
                SET "Version" = role."Version" + 1,
                    "PermissionSetVersion" = rollback."PreviousPermissionSetVersion",
                    "TemplateVersion" = 1,
                    "UpdatedAt" = now()
                FROM "CatalogPolicyV2Rollback" rollback
                WHERE role."Id" = rollback."RoleId";

                DELETE FROM shared."IdentityAuditEvents" audit
                USING "CatalogPolicyV2Rollback" rollback
                WHERE audit."Id" = rollback."AuditId";
                """);
        }
    }
}
