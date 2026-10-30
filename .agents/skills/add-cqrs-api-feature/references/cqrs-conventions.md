# DeliveryPilot CQRS/API conventions — inspect before use

Use these as repository-specific guidance from the supplied root/module documentation; confirm active signatures and registrations against code. This reference intentionally does not copy DevFlow-specific paths.

## Boundaries and layout

- Read `src/Modules/AGENTS.md` for the actual CQRS file layout and API request/error-handling rules; inspect Identity registration and GetUser examples only **if present**. Keep request, handler, validator and response DTO organized according to the existing module conventions.
- Controllers translate HTTP transport input and delegate through `DeliveryPilot.SharedKernel.Cqrs.ISender`; no DbContext, repository, domain service, or identity manager work in controllers. Examine the available API base controllers and their auth requirements first.
- Do not add MediatR or an unrequested mapping library. Inspect existing `ICommand`, `IQuery`, handler, `IValidator`, and `Result` namespaces/signatures before referencing them; the old DevFlow examples are not authority for current DeliveryPilot types.
- Preserve Application → Domain and Infrastructure → Application + Domain dependency direction; never make Application reference Infrastructure. Modules communicate using documented Contracts or Application-owned abstractions, not another module's Domain/DbContext.

## Pipeline, transactions and persistence

- Respect the actual registered pipeline: validation → logging → performance → tracing → transaction → handler. Query behavior does not begin command transactions.
- If transaction behavior owns commit, handlers and repositories do **not** call `SaveChangesAsync`. Check whether the target module registers a real `IUnitOfWork`; do not install a dummy service to hide missing persistence.
- Domain owns invariants and state, not EF mappings or HTTP concerns. Infrastructure owns EF mappings and infrastructure services; API DTOs are separate from persistence entities.
- When appropriate, inspect `EfRepositoryBase<TEntity,TId,TDbContext>` and `IRepositoryBase<TEntity>` implementations. Helpers do not necessarily implement interfaces. Confirm no-tracking versus tracked semantics from current code; don't assume a repository/DbContext exists.
- Keep cross-module IDs as scalar references; avoid cross-module FKs. Use UTC `DateTimeOffset` for stored instants per repository conventions.
- Dapper is optional, never add it for symmetry. Parameterize SQL and whitelist identifiers for dynamic sort operations.

## HTTP and failure behavior

- Read the actual centralized `GlobalExceptionHandlingMiddleware` and returned `Result` mapping; returned failed Results are **not exceptions**. Avoid success-only controllers that swallow failures.
- Prefer centralized error-to-HTTP mapping according to current module guidance. Validate request input through the registered CQRS validation strategy.
- Choose an existing API controller base appropriate to protected/public/integration access; don't invent permission constants, expose a protected route, or assume middleware/registrations are present.
- `CreatedAtAction` requires a real implemented route. Document response status types and validate success, invalid input, denied access, and handler failures through the HTTP boundary.
- For messages/workers, verify current contract schema, broker topology, outbox/inbox/idempotency and actual handlers before changing them. Preserve correlation IDs without logging secrets.

## Verification boundaries

- Use focused solution/project builds and meaningful behavioral tests; run architecture dependency verification if references changed.
- Confirm file-scoped namespace/layout, separate request/handler/validator files where the active module requires them, and API-local request-model placement from current module instructions.
- Never assert a route, service registration, migration, client permission, database or auth policy exists without source evidence.
