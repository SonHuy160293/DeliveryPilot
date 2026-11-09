#!/usr/bin/env bash
# Check Bash module routing with a mock Docker executable; no engine or secret reads are needed.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
fixture=$(mktemp -d)
trap 'rm -f "$fixture/docker" "$fixture/local.env" "$fixture/server.env" "$fixture/calls"; rmdir "$fixture"' EXIT
cat >"$fixture/docker" <<'MOCK'
#!/usr/bin/env bash
printf '%s\n' "$*" >>"$DP025_CLI_CALLS"
if [[ ${1:-} == compose && ${2:-} == version ]]; then printf '2.24.4\n'; fi
MOCK
chmod 700 "$fixture/docker"
export PATH="$fixture:$PATH" DP025_CLI_CALLS="$fixture/calls"
cat >"$fixture/local.env" <<EOF
DELIVERYPILOT_STACK=deliverypilot-verify-bash-cli
DELIVERYPILOT_ENVIRONMENT=Development
DELIVERYPILOT_SECRETS_DIR=$fixture/unused-secrets
EOF
bash "$repo_root/scripts/deliverypilot.sh" status --module projects --config "$fixture/local.env" >/dev/null
grep -Fq -- 'deliverypilot-verify-bash-cli-projects' "$fixture/calls"
grep -Fq -- 'deploy/compose/projects/compose.local.yaml' "$fixture/calls"
if grep -Eq -- 'compose/(identity|notification)/' "$fixture/calls"; then echo 'Projects status touched another business module.' >&2; exit 1; fi
: >"$fixture/calls"
bash "$repo_root/scripts/deliverypilot.sh" status --module all --config "$fixture/local.env" >/dev/null
for module in identity notification projects gateway; do grep -Fq -- "deliverypilot-verify-bash-cli-$module" "$fixture/calls"; done
cat >"$fixture/server.env" <<EOF
DELIVERYPILOT_STACK=deliverypilot-verify-bash-cli
DELIVERYPILOT_ENVIRONMENT=Production
DELIVERYPILOT_SECRETS_DIR=$fixture/unused-secrets
DELIVERYPILOT_MESSAGING_DEVELOPMENT=false
DELIVERYPILOT_MESSAGING_USE_TLS=true
DELIVERYPILOT_MESSAGING_QUORUM_SIZE=3
DELIVERYPILOT_MESSAGING_HOST=broker.example.test
EOF
: >"$fixture/calls"
bash "$repo_root/scripts/deliverypilot.sh" status --module projects --environment server --config "$fixture/server.env" >/dev/null
grep -Fq -- 'deliverypilot-verify-bash-cli-projects' "$fixture/calls"
if grep -Fq -- 'compose.local.yaml' "$fixture/calls"; then echo 'Server routing included local overrides.' >&2; exit 1; fi
printf '%s\n' 'PASS: Bash Projects/all selection, local overrides and server isolation.'
