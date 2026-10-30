#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
action=up environment=local module=all mode=docker telemetry=false config=
if (($#)) && [[ $1 != --* ]]; then action=$1; shift; fi
while (($#)); do
  case "$1" in
    --environment) environment=${2:?Missing environment}; shift 2 ;;
    --module) module=${2:?Missing module}; shift 2 ;;
    --mode) mode=${2:?Missing mode}; shift 2 ;;
    --config) config=${2:?Missing config path}; shift 2 ;;
    --telemetry) telemetry=true; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
fail() { printf '%s\n' "$*" >&2; exit 1; }
[[ $action =~ ^(init|check|up|down|status|logs)$ ]] || fail 'Action must be init, check, up, down, status, or logs.'
[[ $environment =~ ^(local|server)$ && $module =~ ^(all|identity|notification|gateway)$ && $mode =~ ^(docker|host)$ ]] || fail 'Invalid environment, module, or mode.'
[[ $environment != server || $mode != host ]] || fail 'Server uses prebuilt Docker images; host mode is local only.'
[[ -n $config ]] || config="$repo_root/deploy/env/$environment.env"
[[ $config = /* ]] || config="$repo_root/$config"
modules=(identity notification)
[[ $module = all ]] || modules=("$module")
[[ $module != gateway ]] || modules=()
gateway=false
[[ $module != all && $module != gateway ]] || gateway=true
projects=("${modules[@]}")
[[ $gateway != true ]] || projects+=(gateway)
cd "$repo_root"
declare -A settings=()
load_settings() {
  [[ -f $config ]] || fail "Configuration missing: $config. Run init first."
  local line key value
  while IFS= read -r line || [[ -n $line ]]; do
    line=${line%$'\r'}
    [[ $line =~ ^[[:space:]]*(#|$) ]] && continue
    [[ $line =~ ^([A-Z][A-Z0-9_]*)=(.*)$ ]] || fail 'Use literal unquoted KEY=value lines; shell expansion is unsupported.'
    key=${BASH_REMATCH[1]} value=${BASH_REMATCH[2]}
    [[ ! ${settings[$key]+present} ]] || fail "Duplicate configuration key: $key"
    [[ $value != *'$'* && $value != \"* && $value != \'* ]] || fail "Use a literal unquoted value without shell expansion for $key."
    settings[$key]=$value
    export "$key=$value"
  done <"$config"
  [[ ${DELIVERYPILOT_STACK:-} =~ ^deliverypilot-[a-z0-9][a-z0-9-]{0,33}$ ]] || fail 'DELIVERYPILOT_STACK must start with deliverypilot- and contain lowercase letters, digits, and hyphens (maximum 48 characters).'
  [[ -z ${DELIVERYPILOT_BUILD_BUILDER:-} || $DELIVERYPILOT_BUILD_BUILDER =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*$ ]] || fail 'DELIVERYPILOT_BUILD_BUILDER must be a Docker builder name containing only letters, digits, dots, underscores or hyphens.'
  [[ -n ${DELIVERYPILOT_SECRETS_DIR:-} ]] || fail 'DELIVERYPILOT_SECRETS_DIR is required.'
  [[ $DELIVERYPILOT_SECRETS_DIR = /* ]] || DELIVERYPILOT_SECRETS_DIR="$repo_root/$DELIVERYPILOT_SECRETS_DIR"
  export DELIVERYPILOT_SECRETS_DIR DELIVERYPILOT_OBSERVABILITY_ENABLED=$telemetry
  export DELIVERYPILOT_HTTP_SUBNET=${DELIVERYPILOT_HTTP_SUBNET:-172.31.250.0/24} DELIVERYPILOT_GATEWAY_IP=${DELIVERYPILOT_GATEWAY_IP:-172.31.250.10} DELIVERYPILOT_GATEWAY_PORT=${DELIVERYPILOT_GATEWAY_PORT:-19090}
  if [[ $environment = local ]]; then
    export DELIVERYPILOT_GATEWAY_IMAGE=${DELIVERYPILOT_GATEWAY_IMAGE:-deliverypilot-gateway:local} DELIVERYPILOT_GATEWAY_PUBLIC_HOST=${DELIVERYPILOT_GATEWAY_PUBLIC_HOST:-localhost} DELIVERYPILOT_INGRESS_PROXY_IP=${DELIVERYPILOT_INGRESS_PROXY_IP:-172.31.250.1}
  fi
  [[ $environment != local || ${DELIVERYPILOT_ENVIRONMENT:-} = Development ]] || fail 'Local configuration must use Development.'
  if [[ $environment = server && ${#modules[@]} -gt 0 ]]; then
    [[ ${DELIVERYPILOT_ENVIRONMENT:-} = Production && ${DELIVERYPILOT_MESSAGING_DEVELOPMENT:-} = false && ${DELIVERYPILOT_MESSAGING_USE_TLS:-} = true && ${DELIVERYPILOT_MESSAGING_QUORUM_SIZE:-0} =~ ^[3-7]$ ]] || fail 'Server requires Production, messaging development=false, TLS=true, quorum size 3-7.'
    [[ ${DELIVERYPILOT_MESSAGING_HOST:-} != rabbitmq && ${DELIVERYPILOT_MESSAGING_HOST:-} != localhost && ${DELIVERYPILOT_MESSAGING_HOST:-} != 127.0.0.1 ]] || fail 'Server requires an operator-provided trusted TLS RabbitMQ cluster.'
    [[ " ${modules[*]} " != *' notification '* || ${DELIVERYPILOT_EMAIL_MODE:-} != Mailpit ]] || fail 'Mailpit is local only. Server email mode must be Disabled or Resend.'
  fi
}
dc() {
  local project=$1; shift
  local args=(compose --env-file "$config" --project-name "$DELIVERYPILOT_STACK-$project" -f "$repo_root/deploy/compose/$project/compose.yaml")
  if [[ $environment = local && $project != telemetry ]]; then args+=(-f "$repo_root/deploy/compose/$project/compose.local.yaml"); fi
  if [[ $environment = local && $project = platform ]]; then args+=(--profile mailpit); fi
  if [[ $environment = local && ${1:-} = build && -n ${DELIVERYPILOT_BUILD_BUILDER:-} ]]; then
    shift
    docker "${args[@]}" build --builder "$DELIVERYPILOT_BUILD_BUILDER" "$@"
  else
    docker "${args[@]}" "$@"
  fi
}
prerequisites() {
  command -v docker >/dev/null || fail 'Install Docker Engine/Desktop and the Docker Compose plugin.'
  docker info --format 'Docker engine: {{.ServerVersion}}'
  local version major minor patch
  version=$(docker compose version --short)
  [[ $version =~ ([0-9]+)\.([0-9]+)\.([0-9]+) ]] || fail 'Docker Compose >=2.24.4 is required.'
  major=${BASH_REMATCH[1]} minor=${BASH_REMATCH[2]} patch=${BASH_REMATCH[3]}
  ((major > 2 || major == 2 && (minor > 24 || minor == 24 && patch >= 4))) || fail 'Docker Compose >=2.24.4 is required.'
  if [[ $environment = local && $action =~ ^(init|check|up)$ && -n ${DELIVERYPILOT_BUILD_BUILDER:-} ]]; then
    docker buildx inspect "$DELIVERYPILOT_BUILD_BUILDER" >/dev/null || fail 'Configured Docker builder is unavailable. Create it or update DELIVERYPILOT_BUILD_BUILDER.'
  fi
  if [[ $mode = host ]]; then command -v dotnet >/dev/null || fail 'Host mode requires the .NET 10 SDK.'; dotnet --version; fi
}
build_tools() {
  local args=(build --load)
  if [[ -n ${DELIVERYPILOT_BUILD_BUILDER:-} ]]; then args+=(--builder "$DELIVERYPILOT_BUILD_BUILDER"); fi
  docker "${args[@]}" -f "$repo_root/Dockerfile.deploy" --build-arg "POSTGRES_IMAGE=$DELIVERYPILOT_POSTGRES_IMAGE" -t "$DELIVERYPILOT_SETUP_IMAGE" "$repo_root"
}
check_secrets() {
  local required=() name stem relative
  local active_key=${DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID:-local-v1}
  [[ $active_key =~ ^[A-Za-z0-9-]{1,64}$ ]] || fail 'Invalid DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID; use 1-64 ASCII letters, digits, or hyphens.'
  [[ $environment != local || ${#modules[@]} -eq 0 ]] || required+=(platform/broker-admin-password.secret)
  for name in "${modules[@]}"; do
    required+=("platform/$name-broker-password.secret")
    if [[ $name = identity ]]; then
      for stem in admin-password migrator-password api-password worker-password jwt-key integration-key invitation-dispatch-key; do required+=("identity/$stem.secret"); done
      required+=("notification/keys/$active_key.public.pem")
    else
      for stem in admin-password migrator-password runtime-password; do required+=("notification/$stem.secret"); done
      required+=("identity/invitation-dispatch-key.secret")
      required+=("notification/keys/$active_key.private.pem" "notification/keys/$active_key.public.pem" notification/keys/recipient-hmac.secret)
      [[ $DELIVERYPILOT_EMAIL_MODE != Resend ]] || required+=(notification/keys/resend-api.secret notification/keys/resend-webhook.secret)
    fi
  done
  [[ $telemetry != true ]] || required+=(platform/grafana-admin-password.secret)
  for relative in "${required[@]}"; do
    [[ -f $DELIVERYPILOT_SECRETS_DIR/$relative && -r $DELIVERYPILOT_SECRETS_DIR/$relative && -s $DELIVERYPILOT_SECRETS_DIR/$relative ]] || fail "Required secret is missing, empty, or unreadable: $relative"
    grep -q '[^[:space:]]' "$DELIVERYPILOT_SECRETS_DIR/$relative" || fail "Secret contains no value: $relative"
    if grep -qE 'CHANGE_ME|REPLACE_WITH' "$DELIVERYPILOT_SECRETS_DIR/$relative"; then fail "Secret remains a placeholder: $relative"; fi
    [[ $(stat -c %a "$DELIVERYPILOT_SECRETS_DIR/$relative") = *0 ]] || fail "Secret grants access to other users: $relative. Restrict access before continuing."
    if [[ $relative =~ ^(identity|notification)/(admin|migrator|api|worker|runtime)-password.secret$ ]]; then
      [[ $(cat "$DELIVERYPILOT_SECRETS_DIR/$relative") =~ ^[0-9a-fA-F]{64}$ ]] || fail "Database password must be 32 random bytes encoded as hex: $relative"
    fi
    if [[ $relative = identity/jwt-key.secret ]]; then
      (($(tr -d '\r\n' <"$DELIVERYPILOT_SECRETS_DIR/$relative" | wc -c) >= 32)) || fail 'JWT signing key must be at least 32 characters.'
    fi
    if [[ $relative = identity/integration-key.secret ]]; then
      (($(tr -d '\r\n' <"$DELIVERYPILOT_SECRETS_DIR/$relative" | wc -c) <= 4096)) || fail 'Integration key must be at most 4096 characters.'
    fi
  done
}
check_configuration() {
  local key value name kind
  for key in "${!settings[@]}"; do
    if [[ $gateway != true && $key =~ ^DELIVERYPILOT_(GATEWAY_IMAGE|GATEWAY_PUBLIC_HOST|INGRESS_PROXY_IP)$ ]]; then continue; fi
    if [[ ${#modules[@]} -eq 0 && $key =~ ^DELIVERYPILOT_(MESSAGING|JWT|EMAIL)_ ]]; then continue; fi
    if [[ $key =~ ^DELIVERYPILOT_(IDENTITY|NOTIFICATION)_(API|WORKER|MIGRATION)_IMAGE$ ]]; then
      local image_module=${BASH_REMATCH[1],,}
      [[ " ${modules[*]} " = *" $image_module "* ]] || continue
    fi
    value=${settings[$key]}
    [[ ! $value =~ CHANGE_ME|REPLACE_WITH|registry\.example ]] || fail "Replace the placeholder in $key."
    if [[ $key = *_PORT ]]; then [[ $value =~ ^[0-9]+$ ]] && ((10#$value > 0 && 10#$value <= 65535)) || fail "Invalid port: $key"; fi
  done
  if [[ $environment = server ]]; then
    local image_keys=(DELIVERYPILOT_SETUP_IMAGE)
    [[ $gateway != true ]] || image_keys+=(DELIVERYPILOT_GATEWAY_IMAGE)
    for name in "${modules[@]}"; do for kind in API WORKER MIGRATION; do image_keys+=("DELIVERYPILOT_${name^^}_${kind}_IMAGE"); done; done
    for key in "${image_keys[@]}"; do
      value=${settings[$key]:-}
      [[ ! $value =~ :(latest|local|dev|main)$ && $value =~ (@sha256:[a-f0-9]{64}$|:[A-Za-z0-9][A-Za-z0-9_.-]+$) ]] || fail "Use an immutable release tag or digest for $key."
    done
  fi
  dc platform config --quiet
  for name in "${projects[@]}"; do dc "$name" config --quiet; done
  [[ $telemetry != true ]] || dc telemetry config --quiet
  if [[ $gateway = true && $environment = server ]]; then
    [[ -n ${DELIVERYPILOT_INGRESS_PROXY_IP:-} && -n ${DELIVERYPILOT_GATEWAY_PUBLIC_HOST:-} && ! $DELIVERYPILOT_GATEWAY_PUBLIC_HOST =~ [*/:\;[:space:]] ]] || fail 'Server gateway requires an explicit ingress peer IP and a public API hostname.'
    [[ ${DELIVERYPILOT_FRONTEND_ORIGIN:-} =~ ^https://[^/*[:space:]@?#]+$ ]] || fail 'Server frontend origin must be an exact HTTPS origin without credentials, path, query, fragment, or trailing slash.'
  fi
  local ids=() network_json
  mapfile -t ids < <(docker network ls --quiet)
  if ((${#ids[@]})); then network_json=$(docker network inspect "${ids[@]}"); else network_json='[]'; fi
  printf '%s\n' "$network_json" | docker run --rm -i --env-file "$config" --env "DELIVERYPILOT_STACK=$DELIVERYPILOT_STACK" --env "DELIVERYPILOT_HTTP_SUBNET=$DELIVERYPILOT_HTTP_SUBNET" --env "DELIVERYPILOT_GATEWAY_IP=$DELIVERYPILOT_GATEWAY_IP" --env "DELIVERYPILOT_INGRESS_PROXY_IP=$([[ $gateway != true ]] || printf '%s' "$DELIVERYPILOT_INGRESS_PROXY_IP")" "$DELIVERYPILOT_SETUP_IMAGE" check-network
  check_secrets
}
check_ports() {
  local key port name expected owned running
  local -A ports=() seen=()
  if [[ $environment = local && ${#modules[@]} -gt 0 ]]; then
    ports[DELIVERYPILOT_RABBITMQ_AMQP_PORT]=platform; ports[DELIVERYPILOT_RABBITMQ_MANAGEMENT_PORT]=platform
    if [[ " ${modules[*]} " = *' notification '* && $DELIVERYPILOT_EMAIL_MODE = Mailpit ]]; then ports[DELIVERYPILOT_MAILPIT_HTTP_PORT]=platform; ports[DELIVERYPILOT_MAILPIT_SMTP_PORT]=platform; fi
    for name in "${modules[@]}"; do ports[DELIVERYPILOT_${name^^}_POSTGRES_PORT]=$name; done
  fi
  if [[ $mode = docker && $environment = local ]]; then for name in "${modules[@]}"; do ports[DELIVERYPILOT_${name^^}_API_PORT]=$name; done; fi
  if [[ $mode = docker && $gateway = true ]]; then ports[DELIVERYPILOT_GATEWAY_PORT]=gateway; fi
  if [[ $telemetry = true ]]; then for key in DELIVERYPILOT_GRAFANA_PORT DELIVERYPILOT_OTLP_PORT DELIVERYPILOT_OTLP_HTTP_PORT; do ports[$key]=telemetry; done; fi
  running=$(docker ps --format '{{.Names}}|{{.Ports}}')
  for key in "${!ports[@]}"; do
    port=${!key}; expected="$DELIVERYPILOT_STACK-${ports[$key]}"
    [[ ! ${seen[$port]+present} ]] || fail "Port $port is assigned more than once ($key and ${seen[$port]})."
    seen[$port]=$key; owned=false
    while IFS= read -r name; do [[ $name != "$expected-"* || $name != *":$port->"* ]] || owned=true; done <<<"$running"
    [[ $owned != true ]] || continue
    if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then fail "Port $port ($key) is occupied by another process/project. Change it in $config."; fi
  done
}
host_secrets() {
  local name executable project suffix
  if [[ $gateway = true ]]; then
    docker run --rm --env-file "$config" --env "DELIVERYPILOT_HOST_SECRETS_DIR=$DELIVERYPILOT_SECRETS_DIR" --env "DELIVERYPILOT_OBSERVABILITY_ENABLED=$telemetry" --env "DELIVERYPILOT_GATEWAY_PORT=$DELIVERYPILOT_GATEWAY_PORT" "$DELIVERYPILOT_SETUP_IMAGE" host-settings gateway | dotnet user-secrets set --project "$repo_root/src/Gateway/DeliveryPilot.Gateway" >/dev/null
  fi
  for name in "${modules[@]}"; do
    for executable in api worker migrationrunner; do
      case "$executable" in api) suffix=API;; worker) suffix=Worker;; migrationrunner) suffix=MigrationRunner;; esac
      project="$repo_root/src/Modules/DeliveryPilot.${name^}/DeliveryPilot.${name^}.$suffix"
      docker run --rm --mount "type=bind,source=$DELIVERYPILOT_SECRETS_DIR,target=/source,readonly" --env-file "$config" \
        --env "DELIVERYPILOT_HOST_SECRETS_DIR=$DELIVERYPILOT_SECRETS_DIR" --env "DELIVERYPILOT_OBSERVABILITY_ENABLED=$telemetry" \
        "$DELIVERYPILOT_SETUP_IMAGE" host-settings "$name-$executable" | dotnet user-secrets set --project "$project" >/dev/null
    done
  done
  printf '%s\n' 'Selected IDE user-secrets stores configured. Use the Local launch profiles (Development).'
}
if [[ $action = init ]]; then
  if [[ ! -e $config ]]; then mkdir -p -- "$(dirname -- "$config")"; cp -- "$repo_root/deploy/env/$environment.env.example" "$config"; fi
  load_settings; prerequisites
  (umask 077; mkdir -p -- "$DELIVERYPILOT_SECRETS_DIR")
  if [[ $environment = local ]]; then
    build_tools
    if ((${#modules[@]})); then docker run --rm --user "$(id -u):$(id -g)" --mount "type=bind,source=$DELIVERYPILOT_SECRETS_DIR,target=/source" "$DELIVERYPILOT_SETUP_IMAGE" init-secrets; fi
    [[ $mode != host ]] || host_secrets
  else printf '%s\n' 'Server template created. Supply immutable images and operator-provisioned secret files before check/up.'; fi
  printf 'Configuration ready: %s. Existing secrets and data were preserved.\n' "$config"
  exit
fi
load_settings; prerequisites
if [[ $action = check || $action = up ]]; then check_configuration; check_ports; fi
case "$action" in
  check) printf '%s\n' 'Configuration, Compose models, secrets, and selected port bindings passed preflight.' ;;
  up)
    if [[ $environment = local ]]; then
      build_tools
      dc platform run --rm --no-deps network-init
      if ((${#modules[@]})); then
      dc platform build topology
      dc platform up -d --wait --wait-timeout 180 rabbitmq
      dc platform run --rm --no-deps broker-bootstrap
      dc platform run --rm --no-deps topology
      if [[ " ${modules[*]} " = *' notification '* && $DELIVERYPILOT_EMAIL_MODE = Mailpit ]]; then dc platform up -d --wait mailpit; fi
      fi
    else dc platform pull network-init; dc platform run --rm --no-deps network-init; fi
    [[ $telemetry != true ]] || dc telemetry up -d --wait --wait-timeout 180
    for name in "${modules[@]}"; do
      if [[ $environment = local ]]; then
        if [[ $mode = docker ]]; then dc "$name" build migration api worker; else dc "$name" build migration; fi
      else dc "$name" pull secret-stage postgres db-bootstrap migration db-grants api worker; fi
      dc "$name" run --rm --no-deps secret-stage
      dc "$name" up -d --wait --wait-timeout 120 postgres
      dc "$name" run --rm --no-deps db-bootstrap
      dc "$name" run --rm --no-deps migration
      dc "$name" run --rm --no-deps db-grants
      [[ $mode != docker ]] || dc "$name" up -d --no-deps --force-recreate --wait --wait-timeout 180 api worker
    done
    [[ $mode != host ]] || host_secrets
    if [[ $gateway = true && $mode = docker ]]; then
      if [[ $environment = local ]]; then dc gateway build gateway; else dc gateway pull gateway; fi
      dc gateway up -d --wait --wait-timeout 180 gateway
    fi
    printf 'Selected modules ready (%s/%s). Existing data and credentials were preserved.\n' "$environment" "$mode"
    ;;
  down)
    [[ $gateway != true ]] || dc gateway down --remove-orphans
    for name in "${modules[@]}"; do dc "$name" down --remove-orphans; done
    if [[ $module = all ]]; then dc telemetry down --remove-orphans; dc platform down --remove-orphans; fi
    printf '%s\n' 'Selected containers stopped; volumes and source secret files retained.'
    ;;
  status) for name in platform "${projects[@]}" telemetry; do dc "$name" ps --all; done ;;
  logs) for name in platform "${projects[@]}"; do dc "$name" logs --tail 100; done ;;
esac
