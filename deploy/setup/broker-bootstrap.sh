#!/usr/bin/env bash
set -euo pipefail
umask 077
admin_password="$(cat /source/platform/broker-admin-password.secret)"
auth_file="$(mktemp)"
trap 'rm -f "$auth_file"' EXIT
printf 'user = "deliverypilot-admin:%s"\n' "$admin_password" > "$auth_file"
request() { curl --silent --show-error --fail --config "$auth_file" --connect-timeout 5 --max-time 15 -H 'Content-Type: application/json' "$@" >/dev/null; }
base=http://rabbitmq:15672/api
vhost="$(jq -nr --arg vhost "${DELIVERYPILOT_MESSAGING_VIRTUAL_HOST:-deliverypilot}" '$vhost|@uri')"
request -X PUT "$base/vhosts/$vhost" -d '{}'
for module in identity notification projects; do
 # Existing deployments can redeploy Identity before running init to add Projects secrets.
 if [[ "$module" == projects && ! -f /source/platform/projects-broker-password.secret ]]; then continue; fi
 jq -n --rawfile password "/source/platform/$module-broker-password.secret" '{password:($password|rtrimstr("\n")),tags:""}' | request -X PUT "$base/users/$module" --data-binary @-
 read_pattern="^deliverypilot\.$module\."
 write_pattern="(^deliverypilot\.$module\.|^deliverypilot\.messaging\.unrouted$)"
 configure_pattern='^deliverypilot\.'
 if [[ "$module" == projects ]]; then
   configure_pattern="$read_pattern"
   read_pattern='(^deliverypilot\.projects\.|^deliverypilot\.notification\.project-guest-invitation-email-status\.v1$)'
   write_pattern='(^deliverypilot\.projects\.|^deliverypilot\.notification\.project-guest-invitation-email-requested\.v1$|^deliverypilot\.messaging\.unrouted$)'
 fi
 # Exchange binding requires read permission on the incoming receipt exchange.
 # This exact exchange exception does not grant reads of Notification's inbox.
 if [[ "$module" == identity ]]; then
   read_pattern='(^deliverypilot\.identity\.|^deliverypilot\.notification\.email-recorded\.v1$|^deliverypilot\.notification\.organization-invitation-email-status\.v1$)'
   write_pattern='(^deliverypilot\.identity\.|^deliverypilot\.notification\.email-requested\.v1$|^deliverypilot\.notification\.organization-invitation-email-requested\.v1$|^deliverypilot\.messaging\.unrouted$)'
 fi
 jq -n --arg namespace "$configure_pattern" --arg read "$read_pattern" --arg write "$write_pattern" '{configure:$namespace,write:$write,read:$read}' | request -X PUT "$base/permissions/$vhost/$module" --data-binary @-
done
jq -n --arg namespace '^deliverypilot\.' '{pattern:$namespace,definition:{overflow:"reject-publish","max-length":100000,"delivery-limit":20,"dead-letter-exchange":"deliverypilot.messaging.unrouted","dead-letter-strategy":"at-least-once"},priority:10,"apply-to":"quorum_queues"}' | request -X PUT "$base/policies/$vhost/delivery-safety" --data-binary @-
printf '%s\n' 'Broker users, permissions, and safety policy are ready.'
