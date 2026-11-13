#!/usr/bin/env bash
set -euo pipefail
module="${1:?Module required}"
export PGPASSWORD="$(cat "/source/$module/admin-password.secret")"
export PGHOST=postgres PGUSER="${module}_admin"
if [[ "$module" == identity ]]; then
  psql -X -q -v ON_ERROR_STOP=1 -d DeliveryPilotIdentity <<'SQL' >/dev/null
GRANT USAGE ON SCHEMA shared, reliability TO identity_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA shared, reliability TO identity_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA shared, reliability TO identity_api;
GRANT USAGE ON SCHEMA reliability, shared TO identity_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA reliability TO identity_worker;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA reliability TO identity_worker;
GRANT SELECT ON shared."Organizations", shared."OrganizationInvitationDeliveries" TO identity_worker;
GRANT SELECT ("Id", "OrganizationId", "CurrentDeliveryMessageId") ON shared."OrganizationInvitations" TO identity_worker;
GRANT UPDATE ("Status", "LastEventSequence", "FailureCode", "UpdatedAt", "Version") ON shared."OrganizationInvitationDeliveries" TO identity_worker;
REVOKE UPDATE, DELETE ON reliability."OutboxOperations", reliability."OutboxRecoveries" FROM identity_api, identity_worker;
SQL
elif [[ "$module" == notification ]]; then
  psql -X -q -v ON_ERROR_STOP=1 -d DeliveryPilotNotification <<'SQL' >/dev/null
GRANT USAGE ON SCHEMA notification, reliability TO notification_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA notification, reliability TO notification_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA notification, reliability TO notification_runtime;
REVOKE UPDATE, DELETE ON notification."EmailOperations", reliability."OutboxOperations" FROM notification_runtime;
SQL
elif [[ "$module" == projects ]]; then
  psql -X -q -v ON_ERROR_STOP=1 -d DeliveryPilotProjects <<'SQL' >/dev/null
GRANT USAGE ON SCHEMA reliability TO projects_api, projects_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA reliability TO projects_api, projects_worker;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA reliability TO projects_api, projects_worker;
REVOKE UPDATE, DELETE ON reliability."OutboxOperations" FROM projects_api, projects_worker;
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'engagement') THEN
    GRANT USAGE ON SCHEMA engagement TO projects_api;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA engagement TO projects_api;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA engagement TO projects_api;
    IF to_regclass('engagement."EngagementEvents"') IS NOT NULL THEN
      REVOKE UPDATE, DELETE ON engagement."EngagementEvents" FROM projects_api;
    END IF;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'project') THEN
    GRANT USAGE ON SCHEMA project TO projects_api;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA project TO projects_api;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA project TO projects_api;
    IF to_regclass('project."ProjectEvents"') IS NOT NULL THEN
      REVOKE UPDATE, DELETE ON project."ProjectEvents" FROM projects_api;
    END IF;
  END IF;
END $$;
SQL
else exit 2
fi
printf '%s\n' 'Runtime grants refreshed after migrations.'
