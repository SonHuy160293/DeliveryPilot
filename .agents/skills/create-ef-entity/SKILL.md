---
name: create-ef-entity
description: Create or update a DeliveryPilot domain entity, EF Core mapping, and owning module DbContext registration from requested fields or a supplied schema.
---

# Create a DeliveryPilot EF entity

## Establish scope and ownership

When the repository root contains `.codegraph/`, use `codegraph_explore` if
available, or `codegraph explore "<symbol names or question>"` in the shell,
before text search or file reads to locate the owning entity, EF configuration,
DbContext, migrations, and relevant tests. Inspect current source for details;
the index may lag local edits. If no index exists, skip CodeGraph.

Read root `AGENTS.md`, `src/Modules/AGENTS.md`, and any deeper instructions for the
target module. Use `ARCHITECTURE.md` for current backend context; verify the owning
module, project references, contexts, registrations, and migrations in source.
`docs/architecture.md` is a target proposal, not authority to replace independently
hosted modules or introduce planned modules.

When implementing a selected Task, read its linked requirements, scope, readiness,
dependencies, and acceptance criteria. Use `docs/context-map.md` to resolve relevant
business rules and NFRs. Preserve approval status: `ready` is not client approval.
If a Task or supplied DDL conflicts with source or approved requirements, report
the conflict and recommend an artifact revision before implementing the affected
part. Ask only about material unresolved choices; keep independent work moving.

Resolve the owning module and context, entity, key generation, fields, nullability,
lengths, precision, defaults, indexes, relationships, schema, tenant scope, and
concurrency from the request, supplied schema, and existing mappings. Prefer
explicit schema over guesses while checking it against requirements. Reuse existing
entities and configurations instead of creating duplicates.

## Domain and mapping

Place domain models under
`src/Modules/DeliveryPilot.<Module>/DeliveryPilot.<Module>.Domain/Models/` with
namespace `DeliveryPilot.<Module>.Domain.Models`. Use file-scoped namespaces and
one top-level type per file named after the type.

Inspect `Entity<TId>`, `Entity`, `SoftDeletableEntity`, and audit interfaces in
`DeliveryPilot.SharedKernel.Abstractions.Domain` before choosing inheritance.
Some existing models use no shared base; choose only the behavior the entity needs.
Account for inherited audit fields explicitly, including fields ignored by existing
mappings. Verify interceptor registration and query filtering before relying on
audit updates or soft deletion; inheritance alone does not configure either.

Keep invariants in constructors/factories and domain methods; keep EF configuration
in Infrastructure. Domain depends only on BuildingBlocks, without EF attributes,
DbContext, or Infrastructure references. Persist instants as UTC `DateTimeOffset`.
Match established key generation deliberately, including UUID version requirements
and database defaults where applicable.

Place `IEntityTypeConfiguration<TEntity>` in the owning Infrastructure project's
`Persistence/Configurations/` subtree, using the namespace selected by its context.
Preserve plural PascalCase table names, PascalCase columns, and established schemas.
Inspect nearby mappings rather than deriving schema from the module name.
Quote PascalCase identifiers in custom PostgreSQL SQL.

Map required fields, lengths, precision, defaults, keys, relationships, delete
behavior, indexes, and concurrency from the resolved contract. Preserve named,
filtered, composite, and expression indexes; inspect migrations for SQL-only
constraints absent from Fluent configuration. Database constraints support domain
invariants rather than replacing them.

For tenant-owned data, preserve `TenantId` and tenant-scoped uniqueness and
relationships, including composite principal/foreign keys where required. Keep
references to another module as scalar IDs, with no cross-module EF navigations or
database FKs. A tenant column alone does not establish isolation: verify the actual
access enforcement and report missing coverage. Treat proposed RLS/session-setting
work as unimplemented until verified in runtime and database configuration.

## DbContext and runtime

Reuse the owning context, add the `DbSet` when appropriate, and verify configuration
discovery without duplicate registration. Identity and Notification contexts use
namespace predicates in `ApplyConfigurationsFromAssembly`; a new subnamespace is
not automatically included. Preserve the separate Notification delivery and inbox
models: `NotificationInboxDbContext` is registered but unmigrated, and the current
Notification MigrationRunner applies only `NotificationDbContext` migrations.

Both modules currently use PostgreSQL through Npgsql. Reuse their actual persistence
registration, design-time factory, interceptors, and existing `IUnitOfWork` from
`DeliveryPilot.SharedKernel.Abstractions.Persistence`. Trace the active composition
instead of inventing a registration method. Keep Application abstractions separate
from Infrastructure implementations and retain centralized package versions.

For commands dispatched through the custom CQRS pipeline, transaction behavior and
the module unit of work own persistence completion; handlers and repositories must
not call `SaveChangesAsync` independently. Queries do not open command transactions.
For authorized worker or operational changes outside the pipeline, inspect and
preserve that path's persistence ownership and cancellation propagation.

Add contexts, hosts, databases, contracts, or migrations only within authorized
scope and when justified by a reviewed Task and current architecture. Keep
repositories, DTOs, and controllers outside a model-only request. When migrations
are in scope, generate a new migration for the explicit owning context, inspect its
SQL and snapshot changes, and retain existing migration history. Apply schema
changes through the module MigrationRunner/deployment flow, not API startup.

## Verification

Review every requested field across the domain model, mapping, and owning context:
types, nullability, names, keys, defaults, relationships, lengths, precision,
indexes, tenant scope, audit fields, and concurrency. Build affected projects and
run focused existing tests; follow explicit verification constraints. Relevant
references include `tests/DeliveryPilot.Identity.Tests/Persistence/IdentityModelInventoryTests.cs`
and `tests/DeliveryPilot.Notification.Tests/NotificationModelTests.cs`.

Use EF model metadata or generated DDL to check meaningful mapping changes; use
PostgreSQL-backed checks when provider behavior matters. An in-memory provider does
not verify PostgreSQL constraints. Do not create a migration solely for validation
or edit a snapshot by hand to hide pending model changes. If migrations are outside
scope, report any expected snapshot mismatch and deployment work still required.

Run `powershell -File scripts/Test-Architecture.ps1` after project-reference or
architecture-boundary changes when available. For documentation-only changes, run
`python tools/docs.py validate` when that tool exists. Report modified files, checks
actually run, unresolved schema choices, blockers, and unmet acceptance criteria;
distinguish model registration from deployed schema or a working runtime feature.
