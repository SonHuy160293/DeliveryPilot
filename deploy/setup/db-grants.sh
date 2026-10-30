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
else exit 2
fi
printf '%s\n' 'Runtime grants refreshed after migrations.'
