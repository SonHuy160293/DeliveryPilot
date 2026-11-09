# Run DeliveryPilot locally

The current checkout contains a backend: a gateway, Identity, Notification and Projects APIs and workers, PostgreSQL databases, RabbitMQ and a local Mailpit mailbox. Run the separate `DeliveryPilot-Web` frontend checkout alongside this deployment for browser workflows. Run backend commands from this repository root. The examples use Windows PowerShell; Linux users can use the Bash equivalents below.

The Gateway publishes the explicit Organization allowlist under `/identity/api/v1`, including workspace creation/listing, permissions, roles/retirement, member management, invitations, ownership nominations and audit history. Use the exact methods in `src/Gateway/DeliveryPilot.Gateway/proxy.json`; there is no catch-all or public integration/dispatch route. Identity validates bearer sessions and current workspace authority. New Organization routes have a 16 KB body ceiling, a 30-second timeout and no-store responses, including errors. Browser CORS permits GET/POST/PUT and the Idempotency-Key header for workspace creation; configure the exact trusted frontend origin for each environment.

Organization publication acceptance extends `scripts/Test-GatewayDeployment.ps1 -Config <disposable-config>` with all 30 protected routes, unsupported-method/private-path checks, no-store and destination-outage checks. `scripts/Test-Deployment.ps1 -Config <disposable-config>` invokes that check and the deployed Notification smoke tests. These scripts require a disposable `deliverypilot-verify-*` Development/Mailpit stack and are not production release commands. Detailed backend acceptance and external UI/Project handoffs are recorded under FEAT-DP-003 task 014.

## Prerequisites

1. Install and start Docker Desktop with Linux containers, or Docker Engine with the Compose plugin (Compose 2.24.4 or newer). Confirm `docker info` and `docker compose version` work for your user. Docker needs access to download the pinned images and restore packages when building.
2. Clone the repository and open a terminal in its root. Ensure the configured local ports and Docker subnet are available. If necessary, change the non-secret values in `deploy/env/local.env` after initialization; use literal, unquoted `KEY=value` lines.
3. For host development, also install a .NET 10 SDK compatible with `global.json` (10.0.301, with `latestFeature` roll-forward). Docker-only operation does not require a host SDK.

## Run the entire backend in Docker

1. Initialize configuration and local secrets:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 init
   ```

   This copies `deploy/env/local.env.example` to `deploy/env/local.env` if absent, builds the setup image and generates secrets under `deploy/secrets/deliverypilot-local`. Existing configuration and secrets are retained. Keep secrets private and out of version control.

2. Check configuration, secrets, Compose models, network and ports:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 check
   ```

3. Build and start the stack:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 up
   ```

   The first build and image downloads can take several minutes. The script initializes networking and RabbitMQ, starts PostgreSQL, applies migrations and grants, then starts the APIs, workers and gateway. Wait for the success message; investigate errors rather than assuming startup completed.

4. Inspect status and readiness:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 status
   Invoke-WebRequest -UseBasicParsing http://localhost:19090/health/ready
   Invoke-WebRequest -UseBasicParsing http://localhost:19091/health/ready
   Invoke-WebRequest -UseBasicParsing http://localhost:19092/health/ready
   Invoke-WebRequest -UseBasicParsing http://localhost:19093/health/ready
   ```

   Each readiness request should return HTTP 200. Default addresses, configurable in `local.env`, are:

   | Service | Address |
   | --- | --- |
   | Gateway | http://localhost:19090 |
   | Identity API | http://localhost:19091 |
   | Notification API | http://localhost:19092 |
   | Projects API | http://localhost:19093 |
   | Mailpit inbox | http://localhost:28025 |
   | RabbitMQ management | http://localhost:25673 |
   | Identity PostgreSQL | localhost:25431 |
   | Notification PostgreSQL | localhost:25432 |
   | Projects PostgreSQL | localhost:25433 |

   Mailpit captures local email. RabbitMQ credentials are generated in the local secrets directory; do not paste them into logs or documentation.

5. To inspect logs, restart after code changes, or stop containers:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 logs
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 up
   powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 down
   ```

   `up` rebuilds local images and recreates selected application containers. `down` retains Docker volumes and source secrets; a later `up` reuses the data. Do not remove volumes unless you intend to erase local database data.

For Linux, use the same actions with Bash:

```bash
bash scripts/deliverypilot.sh init
bash scripts/deliverypilot.sh check
bash scripts/deliverypilot.sh up
bash scripts/deliverypilot.sh status
bash scripts/deliverypilot.sh logs
bash scripts/deliverypilot.sh down
```

## Run the frontend and test an invitation

In `deploy/env/local.env`, use `DELIVERYPILOT_FRONTEND_ORIGIN=http://127.0.0.1:5173` and retain `DELIVERYPILOT_EMAIL_MODE=Mailpit` for local capture. Existing env files are preserved by `init`, so update the origin explicitly and rerun `up` to apply it to Notification, Identity and Gateway. Older captured emails retain their original links.

In the separate `DeliveryPilot-Web` repository, copy `.env.example` to `.env.local` if needed. Its `/identity/` API base and development proxy target `http://127.0.0.1:19090` forward requests to this Gateway without stripping the prefix. Run `npm run dev` and open `http://127.0.0.1:5173`; the frontend reserves that port and fails rather than silently changing the email landing origin.

Use disposable accounts and a workspace to issue an invitation from the frontend. Open its new message at `http://localhost:28025` and follow the review link. An existing account signs in, previews the offer, then explicitly accepts or declines. A new recipient registers, signs in, then reopens the original email: registration creates no session or membership and retires the invitation proof. Reload and account changes also require reopening the email. Acceptance checks current membership before entering the workspace.

Mailpit captures email locally and does not send to real inboxes. Real delivery uses separately configured Resend credentials, a verified sender/domain and an HTTPS frontend origin reachable by the recipient. Do not place provider secrets in frontend configuration. Use trusted local HTTPS if the browser requires it for Secure cookies; do not relax cookie flags.

## Run application processes on the host

This mode keeps PostgreSQL, RabbitMQ and Mailpit in Docker, applies migrations in containers, and configures .NET user secrets for the host processes. Stop any existing Docker application containers first; they use the same API and gateway ports. From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 down
powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 init -Mode host
powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 check -Mode host
powershell -ExecutionPolicy Bypass -File scripts/deliverypilot.ps1 up -Mode host
dotnet restore DeliveryPilot.slnx
dotnet build DeliveryPilot.slnx
```

Open seven terminals in the repository root and run one command in each. Keep them running:

```powershell
dotnet run --project src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Worker --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.API --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Worker --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Projects/DeliveryPilot.Projects.API --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Projects/DeliveryPilot.Projects.Worker --launch-profile Local
dotnet run --project src/Gateway/DeliveryPilot.Gateway --launch-profile Local
```

The addresses and readiness checks above apply here too. The migration runners have already run during `up`; they are not long-running processes. Stop each host process with Ctrl+C, then use the `down` command to stop the infrastructure. On Linux, replace the PowerShell script calls with `bash scripts/deliverypilot.sh <action> --mode host`.

## Troubleshooting and options

- PowerShell failures identify the Docker operation and service, for example `Docker Compose platform build (topology) failed`. The underlying Docker diagnostic appears immediately before it; the wrapper omits raw command arguments and configuration values.
- If Docker cannot connect, start Docker Desktop/Engine and check the current user's access. If a port or subnet conflicts, update the corresponding values in `deploy/env/local.env`, then rerun `check` and `up`.
- Use `status` and `logs` to locate a failing service; readiness can fail while a database or migration is still starting. Verify internet access if image pulls or package restore fail.
- To select a component, add `-Module identity`, `-Module notification`, `-Module projects`, or `-Module gateway` to PowerShell actions (Bash: `--module identity`, etc.). The default is `all`. A gateway needs its downstream APIs available.
- Add `-Telemetry` (Bash: `--telemetry`) to `init`, `check`, and `up` for the optional observability stack. The default Grafana port is 23000. Use the same flag when configuring host mode.
- Local settings are for development. Server deployment requires separately provisioned secrets, immutable images, a TLS RabbitMQ cluster and server-specific configuration; consult `deploy/env/server.env.example` and the deployment scripts before using `-Environment server`.

### Missing Docker build-cache snapshots

An error such as `failed to compute cache key: failed to stat parent ... snapshotter ... no such file or directory` can indicate missing state in Docker Desktop's default builder, even while existing application containers remain healthy. Check the failed build in Docker Desktop or `docker buildx history` to distinguish this from a source/restore failure.

Use a dedicated local builder with its own cache when that failure is confirmed:

```powershell
docker buildx create --name deliverypilot-local-build --driver docker-container --driver-opt default-load=true --bootstrap
```

If that builder already exists, inspect it with `docker buildx inspect deliverypilot-local-build --bootstrap` instead of recreating it. The `default-load` driver option requires Buildx 0.14 or newer. Add this non-secret setting to your local env file:

```text
DELIVERYPILOT_BUILD_BUILDER=deliverypilot-local-build
```

Both launch scripts use this optional setting for setup-image and Compose builds. They validate that the builder exists during local `init`, `check` and `up`; they do not create it or change the globally selected builder automatically. Server deployments still pull prebuilt images. Rerun `check` and `up` after configuration.

The dedicated builder bypasses the damaged build cache; it does not repair that cache or require deletion of application containers, database volumes or secrets. Keep those separate from any later Docker cache maintenance. See Docker's [container builder documentation](https://docs.docker.com/build/builders/drivers/docker-container/) and [Compose builder selection](https://docs.docker.com/reference/cli/docker/compose/build/).

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-DeploymentCli.ps1` for offline argument-routing and safe-error regression checks. The full disposable-stack acceptance suite remains `scripts/Test-Deployment.ps1`.

## Projects engagement creation and deployment

Projects exposes `POST /api/v1/organizations/{organizationId}/engagements` directly on its local API port. Supply a real Identity bearer token and a nonempty UUID `Idempotency-Key`. The user must currently hold `organization.engagements.create` in the route workspace. Required JSON fields are `clientDisplayName`, `agreementReference` and `agreementSignedOn`; optional fields are `clientLegalName`, `clientCountryCode`, `engagementName` and `documentUrl`. Creation returns 201; replay returns 200 with the original creation snapshot and duplicate warning. Reusing a key for different normalized content returns 409. Invalid fields return 400, inactive sessions 401, denied authority 403 and unavailable Identity 503. Responses are no-store and bodies are limited to 16 KB.

Full-detail readers can use `GET` on the engagement collection, an engagement ID, or its `/history` path. The list and history accept `limit` and `cursor`; the list also accepts `status=active|closed|all`. Read-all and manage holders see the workspace; other members see their own creations. Manage holders can update `/contract-value`, manage `/milestones`, and call `/close` or `/reopen`. Those mutations use expected versions as documented in their tasks.

Projects Gateway routes belong to TASK-DP-025-013. Server Compose publishes no Projects API or PostgreSQL port. Local ports are controlled by `DELIVERYPILOT_PROJECTS_API_PORT` (19093) and `DELIVERYPILOT_PROJECTS_POSTGRES_PORT` (25433); verification defaults are 29093/35433. Image variables are `DELIVERYPILOT_PROJECTS_API_IMAGE`, `DELIVERYPILOT_PROJECTS_WORKER_IMAGE` and `DELIVERYPILOT_PROJECTS_MIGRATION_IMAGE`. Server deployments use prebuilt release images. The internal Identity URL defaults to `http://identity-api:8080/` and may be set with `DELIVERYPILOT_PROJECTS_IDENTITY_BASE_URL`; host mode uses Identity's loopback port. The `all` selection includes Projects after Identity and Notification. Selecting Projects alone preserves the other module containers and expects Identity to be available for protected requests.

When upgrading an existing local deployment, rerun `init` to add Projects credentials while preserving existing files. Existing local env files receive default Projects image/port values in the CLI; add explicit settings if different ports are needed. Add Projects release image settings explicitly to existing server env files. Provision `projects/admin-password.secret`, `migrator-password.secret`, `api-password.secret`, `worker-password.secret`, `integration-key.secret`, and `platform/projects-broker-password.secret` in the server secret directory.

Projects API loads key-per-file settings from `/run/secrets/config` before service registration. `DELIVERYPILOT_CONFIG_SECRETS_DIRECTORY` overrides that directory for host tests. Deployment stages Identity's canonical JWT key as `Jwt__SecretKey`, Identity's integration key as `Integrations__Identity__ApiKey`, and Projects' separate inbound key as `IntegrationApi__ApiKeys__0`. The worker receives its database/broker files and the migrator its database file. Keys are not embedded in images or supplied as command arguments/environment values. JWT rotation updates Identity's canonical file, then restages and restarts both APIs; keys load at startup.

The database/migration owner is `projects_migrator`. The API role can select/insert engagement events but cannot update, delete or truncate them, and cannot create schema objects. The worker has reliability access without engagement table grants. Startup runs migrations and refreshes grants before replacing runtime containers. Re-run grants after future migrations; engagement grants tolerate a reliability-only schema. `down` retains database volumes and source credentials. Correct a failed deployment's configuration/credentials and rerun `up`; application rollback uses previous images and retains data rather than automatically downgrading the database.

Optional telemetry loads `deploy/observability/projects-alerts.yaml` into Prometheus. Rules select `service_name="DeliveryPilot.Projects.Worker"` for stalled outbox entries, dead letters and stale scan heartbeat. Email-specific operational rules stay in `deploy/delivery/alerts.yaml`.

Run `scripts/Test-Deployment.ps1 -Config <disposable-config>` for full regression and Projects acceptance; it invokes `Test-ProjectsDeployment.ps1`, covering real Identity authentication, idempotency, request limits, CORS, runtime grants, secret isolation, independent redeploy, retained data and failure gating. These scripts accept only disposable `deliverypilot-verify-*` stacks. Run `scripts/Test-DeploymentCli.ps1` and `bash scripts/Test-DeploymentCli.sh` for CLI routing checks. Server verification renders the image-only profile; live production rollout uses operator-supplied images, credentials and broker configuration.

For a new disposable configuration, copy `deploy/env/verify.env.example` to the ignored `deploy/env/verify.env`, choose unused ports/subnet, then pass `-Config deploy/env/verify.env` to the acceptance script. Existing local env files are preserved. Validate Projects alert syntax and module scoping with `docker run --rm --mount "type=bind,src=<absolute-repo-path>/deploy/observability,dst=/rules,readonly" --workdir /rules --entrypoint /bin/promtool prom/prometheus:v3.2.1 test rules projects-alerts.test.yaml`.
