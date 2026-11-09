using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-DP-003-022 / FEAT-DP-025 OQ-025-08: grants the catalog policy version 3 code
    /// <c>organization.engagements.manage</c> to the built-in Owner and Admin roles of existing organizations.
    /// Each upgraded role advances its permission-set version (so pending invitation offers of that role must be
    /// reissued) and records one system-attributed <c>organization.role.permissions-changed</c> audit event.
    /// Retired and custom roles are not changed.
    /// </summary>
    public partial class UpgradeBuiltInRolesToCatalogPolicyV3 : Migration
    {
        private const string UpgradeReference = "catalog-policy-v3-upgrade";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "CatalogPolicyV3Targets" ON COMMIT DROP AS
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
                        AND existing."PermissionCode" = 'organization.engagements.manage');

                INSERT INTO shared."OrganizationRolePermissions" ("OrganizationId", "RoleId", "PermissionCode", "CatalogPolicyVersion")
                SELECT target."OrganizationId", target."RoleId", 'organization.engagements.manage', 3
                FROM "CatalogPolicyV3Targets" target
                ON CONFLICT DO NOTHING;

                UPDATE shared."OrganizationRoles" role
                SET "Version" = target."PreviousVersion" + 1,
                    "PermissionSetVersion" = target."PreviousPermissionSetVersion" + 1,
                    "TemplateVersion" = 3,
                    "UpdatedAt" = now()
                FROM "CatalogPolicyV3Targets" target
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
                           'templateVersion', 3,
                           'previousPermissionSetVersion', target."PreviousPermissionSetVersion",
                           'permissionSetVersion', target."PreviousPermissionSetVersion" + 1,
                           'addedPermissionCodes', jsonb_build_array('organization.engagements.manage'),
                           'removedPermissionCodes', '[]'::jsonb,
                           'permissionCodes', (
                               SELECT to_jsonb(array_agg(code ORDER BY code COLLATE "C"))
                               FROM unnest(target."PreviousCodes" || ARRAY['organization.engagements.manage']) AS code))
                FROM "CatalogPolicyV3Targets" target;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback: removes the upgraded grants and the upgrade audit events, restores the previous
            // permission-set and template versions, and advances Version so optimistic concurrency stays monotonic.
            // Grants of the new code made to custom roles after the upgrade are left in place; the version 2
            // catalog does not recognize them, so they stay ineffective.
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "CatalogPolicyV3Rollback" ON COMMIT DROP AS
                SELECT audit."Id" AS "AuditId",
                       audit."AggregateId" AS "RoleId",
                       (audit."Details" ->> 'previousPermissionSetVersion')::bigint AS "PreviousPermissionSetVersion"
                FROM shared."IdentityAuditEvents" audit
                WHERE audit."EventType" = 'organization.role.permissions-changed'
                  AND audit."ActorKind" = 'system'
                  AND audit."ActorReference" = '{UpgradeReference}';

                DELETE FROM shared."OrganizationRolePermissions" permission
                USING "CatalogPolicyV3Rollback" rollback
                WHERE permission."RoleId" = rollback."RoleId"
                  AND permission."PermissionCode" = 'organization.engagements.manage';

                UPDATE shared."OrganizationRoles" role
                SET "Version" = role."Version" + 1,
                    "PermissionSetVersion" = rollback."PreviousPermissionSetVersion",
                    "TemplateVersion" = 2,
                    "UpdatedAt" = now()
                FROM "CatalogPolicyV3Rollback" rollback
                WHERE role."Id" = rollback."RoleId";

                DELETE FROM shared."IdentityAuditEvents" audit
                USING "CatalogPolicyV3Rollback" rollback
                WHERE audit."Id" = rollback."AuditId";
                """);
        }
    }
}
