# DeliveryPilot Context Routing Map

This is a navigation index, **not** a duplicate Product Blueprint. Load only the material that affects the requested operation, expanding when business or security correctness requires it.

| Question | Location and authority |
|---|---|
| Why this product and for whom? | [Product definition and customers](product.md) |
| What belongs in MVP? | [Product MVP](product.md) and [Roadmap](roadmap.md) |
| Feature registry, priorities and sequencing | [Roadmap](roadmap.md) |
| Cross-feature business rules and artifact approvals | [Requirements](requirements.md) |
| Global NFR identifiers and security expectations | [Requirements](requirements.md) |
| Confirmed end-to-end workflow and its provenance | [Workflow discovery](deliverypilot-workflow-discovery.md), Q1–Q44 and W1; derived clarifications A1–A4 remain proposals |
| Current backend structure and hosting | [Current backend architecture](../ARCHITECTURE.md), verified against source, tests and deployment configuration |
| Target topology and technology alternatives | [Target architecture proposal](architecture.md); does not replace the current hosts |
| Selected module ownership, ERD and outstanding design decisions | [Architecture workspace](architecture/README.md); currently [Identity & Organization](architecture/modules/identity/README.md) |
| Tenant ownership and resource authorization | [Requirements](requirements.md#3-identity-ownership-and-collaboration), [Identity decisions](architecture/modules/identity/design-decisions.md), and the owning feature's resource policy |
| Workspace / Organization and scoped access terminology | [Global glossary](requirements.md#2-domain-glossary) and [Identity glossary](architecture/modules/identity/GLOSSARY.md) |
| Documentation conflicts, corrections and remaining handoffs | [Alignment audit](architecture/documentation-alignment-audit.md) |
| Exact feature business scope and candidate decisions | `features/FEAT-DP-XXX-<slug>/feature.md` |
| Detailed requirement, story or task | Resolve the artifact ID under the relevant `ur/`, `fr/`, `us/` or `tasks/` |
| Canonical format | [Templates](templates/) and [Authoring policy](authoring.md) |
| Current source behavior | Actual source, tests, OpenAPI and migrations (not prose assumptions) |
| Implementing a ready backend CQRS/API Task | [CQRS implementation skill](../.agents/skills/add-cqrs-api-feature/SKILL.md); task ID and linked source requirements |
| Implementing a scoped domain model and EF mapping | [Entity skill](../.agents/skills/create-ef-entity/SKILL.md), selected module dictionary, current mappings and explicit implementation scope |

## Context-loading policy

1. Read root `AGENTS.md` plus this file first. For feature creation read the roadmap row, relevant product/requirements sections, the workflow's [feature mapping](deliverypilot-workflow-discovery.md#161-existing-feature-registry-mapping), and related feature summaries. For design, follow the architecture workspace to the selected module; expand only as necessary.
2. For UR creation prioritize evidence, actors and business rules; do not load the whole source tree. For FR load source URs; for US load linked FRs and relevant behaviors.
3. For task creation read the selected US/FR/NFR and relevant architecture sections **and inspect current code/tests**; do not invent namespaces, API schemas or database structures from the plan.
4. Skills provide procedure; approved requirements define intended behavior; accepted architecture decisions constrain design; source/tests/contracts establish implementation reality. Within `docs/`, distinguish user-confirmed discovery, user-selected design, proposals, and historical alternatives. A later file is not automatically more authoritative. Do not embed mutable copies of product facts in skills.
5. Record unresolved high-impact choices and their affected scope; do not reopen an answered question because an older summary still lists it as open. Never promote discovery or generated text to artifact approval or implementation evidence.
6. Repository FR→UR / US→FR frontmatter is an authoring contract. Runtime direct UR-to-Story links are a separate product capability; see [authoring boundaries](authoring.md#repository-artifacts-versus-product-runtime).
