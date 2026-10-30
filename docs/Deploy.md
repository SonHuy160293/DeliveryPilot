# Run DeliveryPilot locally

The current checkout contains a backend: a gateway, Identity and Notification APIs and workers, PostgreSQL databases, RabbitMQ and a local Mailpit mailbox. There is no frontend in this deployment. Run commands from the repository root. The examples use Windows PowerShell; Linux users can use the Bash equivalents below.

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
   ```

   Each readiness request should return HTTP 200. Default addresses, configurable in `local.env`, are:

   | Service | Address |
   | --- | --- |
   | Gateway | http://localhost:19090 |
   | Identity API | http://localhost:19091 |
   | Notification API | http://localhost:19092 |
   | Mailpit inbox | http://localhost:28025 |
   | RabbitMQ management | http://localhost:25673 |
   | Identity PostgreSQL | localhost:25431 |
   | Notification PostgreSQL | localhost:25432 |

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

Open five terminals in the repository root and run one command in each. Keep them running:

```powershell
dotnet run --project src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Worker --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.API --launch-profile Local
dotnet run --project src/Modules/DeliveryPilot.Notification/DeliveryPilot.Notification.Worker --launch-profile Local
dotnet run --project src/Gateway/DeliveryPilot.Gateway --launch-profile Local
```

The addresses and readiness checks above apply here too. The migration runners have already run during `up`; they are not long-running processes. Stop each host process with Ctrl+C, then use the `down` command to stop the infrastructure. On Linux, replace the PowerShell script calls with `bash scripts/deliverypilot.sh <action> --mode host`.

## Troubleshooting and options

- PowerShell failures identify the Docker operation and service, for example `Docker Compose platform build (topology) failed`. The underlying Docker diagnostic appears immediately before it; the wrapper omits raw command arguments and configuration values.
- If Docker cannot connect, start Docker Desktop/Engine and check the current user's access. If a port or subnet conflicts, update the corresponding values in `deploy/env/local.env`, then rerun `check` and `up`.
- Use `status` and `logs` to locate a failing service; readiness can fail while a database or migration is still starting. Verify internet access if image pulls or package restore fail.
- To select a component, add `-Module identity`, `-Module notification`, or `-Module gateway` to PowerShell actions (Bash: `--module identity`, etc.). The default is `all`. A gateway needs its downstream APIs available.
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
