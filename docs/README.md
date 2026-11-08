# DeliveryPilot documentation

> Status: Current navigation and authority index. Reviewed 2026-11-08.
> Authority: Routes to document owners; this index does not approve requirements or establish deployment state.

## Current reading paths

| Reader / question | Read in order | Authority and currency |
|---|---|---|
| Product direction and boundaries | [Product](product.md) → [SDLC workflow](sdlc-workflow.md) → [Roadmap](roadmap.md) | D1–D6 establish post-contract fixed-price direction and eight overlapping stages. Roadmap release allocations remain proposals. |
| Business policy and delivery decisions | [Requirements](requirements.md) → relevant feature contracts | Explicitly labelled user choices versus proposals; approved artifacts require version-specific evidence. |
| Why a decision changed | [Discovery decision record](deliverypilot-workflow-discovery.md#19-discovery-decision-record) → [alignment audit](architecture/documentation-alignment-audit.md) | Dated provenance; earlier alternatives apply only where not superseded. |
| Current backend and deployment | [Backend architecture](../ARCHITECTURE.md) → [deployment guide](Deploy.md) → current source/contracts | Independently hosted components. Deployment instructions do not establish the state of an environment. |
| Identity persistence and implementation | [Identity baseline](architecture/modules/identity/README.md#verified-implementation-baseline) → [current schema](architecture/modules/identity/current-schema.md) | Source reviewed 2026-11-08; no live database verification. |
| Organization API integration | [Gateway contract](features/FEAT-DP-003-organization/gateway-api-contract.md) → [frontend playbook](features/FEAT-DP-003-organization/frontend-integration.md) → [backend acceptance](features/FEAT-DP-003-organization/backend-acceptance.md) | Existing wire contract and dated verification. Frontend/Project handoffs are explicitly separate. |
| Future architecture | [Architecture workspace](architecture/README.md) → [target alternatives](architecture.md) | Selected Identity ownership is distinct from unselected hosting/module proposals. |
| Research and customer validation | [Industry research](research/README.md) → [product analysis](research/analysis/README.md) | Evidence and hypotheses. Presales research and example figures do not expand initial product scope. |
| Authoring and task execution | [Context map](context-map.md) → [authoring policy](authoring.md) → [templates](templates/) | Stable IDs, forward traceability, readiness and approval rules. Task execution requires fresh source/readiness review. |

## Inventory and document classes

The reconciliation reviewed 122 existing Markdown files and adds this index plus the current-schema reference. Preserve one canonical owner per mutable fact rather than synchronizing many summaries manually.

| Document group | Class and use |
|---|---|
| Product, requirements, lifecycle, roadmap, routing and authoring | Current narrative references; local proposal labels and detailed artifact approvals still apply. |
| `features/*/{ur,fr,us,tasks}` and feature summaries | Canonical individual contracts with their existing metadata. `ready`, `approved`, implementation `done`, and deployed are different claims. |
| Organization API/playbook and backend acceptance | Integration references and recorded evidence; test results retain original revision/environment. |
| Original research and analysis | Industry context and proposed synthesis; no interviews or measured pain frequencies are claimed. |
| Identity decisions and glossary | Selected business vocabulary and decision provenance; dated implementation observations route to the current baseline. |
| Identity original discovery/readiness, ERD/dictionary, Organization audits/backend plan | Historical analyses and physical/planning proposals. Use their replacement links before acting on old blockers, counts or table definitions. |
| Target architecture | Unselected alternatives except explicitly attributed ownership decisions; does not authorize host consolidation. |
| Templates | Authoring formats, not real artifact instances or product policy. |

## Meaning of completion

- A document review checks consistency; it does not grant business approval.
- A done backend Task records its bounded implementation; frontend, external resources and deployment may remain separate.
- A source-backed schema describes checked-out persistence, not a live environment.
- A historical test result remains evidence for its recorded revision; rerunning checks produces new evidence.

Unresolved product questions remain in the [validation guide](research/analysis/08-assumptions-and-interviews.md). Topology selection and future module placement require explicit architecture decisions. These are tracked decisions, not contradictions to hide through wording.
