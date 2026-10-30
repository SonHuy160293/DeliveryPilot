# Module rules

The root `AGENTS.md` also applies to this directory. This file owns module coding conventions only. The current runtime surface, dependency direction and known gaps live in [`ARCHITECTURE.md`](../../ARCHITECTURE.md). Inspect the active hosts and source before assuming a model or endpoint exists.

## Module boundaries

- `DeliveryPilot.Identity/` owns authentication (users, password credentials, sessions and JWT issuance), Organization Management (workspaces, memberships, roles, invitations) and Identity audit events. Its worker runs the outbox relay and the delivery receipt and status consumers.
- `DeliveryPilot.Notification/` owns notification delivery, templates, provider outcomes and suppression. It delivers messages staged by other modules and does not verify accounts.

Integrate across modules through Contracts or Application-owned abstractions, never through another module's Domain or Infrastructure. Register new projects in `DeliveryPilot.slnx`, and update architecture checks when introducing a new project kind or boundary.

## Layer ownership

- API owns HTTP transport, middleware, host configuration and composition.
- Application owns use cases, commands, queries, handlers, validators, DTOs, and store or integration abstractions. Organize use cases by feature.
- Domain owns entities, value objects, invariants and domain events.
- Infrastructure owns EF contexts and mappings, stores and repositories, and external adapters.
- Worker composes independently deployed background work for its own module; it does not depend on another module's internals or executable hosts.
- MigrationRunner applies its module's migrations. Never migrate implicitly at API startup.

Place use-case-specific store or repository interfaces in Application and their implementations in Infrastructure. Do not add generic repository base types. Persist only through the pipeline-owned `IUnitOfWork` transaction for commands; handlers, stores and repositories never call `SaveChangesAsync` themselves.

Workspace-scoped use cases authorize through `IOrganizationAuthorization` (reads) or `IOrganizationMutationGuard` (commands, inside the pipeline transaction) before touching data. Store queries always filter by `OrganizationId`. Never derive tenant, role or permission authority from token claims or request input.

## CQRS file layout

Place each Application use case in its own folder:

- Commands: `Features/<Area>/Commands/<UseCase>/`.
- Queries: `Features/<Area>/Queries/<UseCase>/`.

Keep the request, handler, validator when needed, and local response DTOs together, with one top-level type per `.cs` file named after the type and a matching namespace. Apply this to API, worker and consumer use cases. For a query reference, inspect `src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.Application/Features/Users/Queries/GetUser/`.

When moving existing types, update callers and namespaces and preserve assembly discovery, public signatures, validation and handler behavior. Verify every changed command and query follows this layout.

## API request contracts and failure handling

Place ordinary HTTP request models in the owning API project's `Contracts/<Area>/<RequestName>.cs`, one top-level type per file with a matching namespace. Map them to Application commands or queries; these API-local contracts are distinct from cross-module contracts under `src/Contracts/`. Controllers send use cases through `ISender` and select the HTTP status. Handlers prepare response DTOs; controllers return the handler value without mapping response data.

Signal expected failures by throwing the matching `DomainException` subtype (`NotFoundException`, `ForbiddenException`, `ConflictException`, `UnauthorizedException`, `ServiceUnavailableException`) or `CqrsValidationException`, and let them reach `GlobalExceptionHandlingMiddleware`. Do not add new `Result.Failure` paths: Identity handlers currently return only successful `Result` values, and the middleware does not inspect returned failures. Never read `result.Value` on a failed result; it throws `InvalidOperationException`, which surfaces as an unhandled 500. Trace validation, pipeline, handler and middleware together when changing a failure path.

Preserve special transport boundaries. The Notification Resend webhook authenticates the raw request and reads a verified event from `HttpContext.Features`; preserve its verification and failure responses when changing that route. Before completing an API change, verify that invalid input and handler failures return the intended status, error details and trace ID, and preserve existing public response behavior.

---

Last verified against source: 2026-11-05, commit `54d6425`.
