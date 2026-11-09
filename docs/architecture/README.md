# Architecture workspace

Use this directory for scoped design evidence, selected ownership and module data models. A design choice, a proposed physical schema and implemented runtime behavior have different authority.

| Source | What it owns / how to read it |
|---|---|
| [Current backend architecture](../../ARCHITECTURE.md) | Existing independently hosted Identity, Notification and Gateway structure as documented; verify against source/tests/deployments. |
| [Target architecture proposal](../architecture.md) | Modular-monolith and technology alternatives. Selected Identity ownership does not authorize host consolidation or replace existing infrastructure. |
| [Workflow discovery](../deliverypilot-workflow-discovery.md) | Q1–Q44/W1 product workflow evidence, with derived clarifications and unresolved policy explicitly labeled. |
| [Identity & Organization module](modules/identity/README.md) | Selected combined Authentication, Organization Management and protected Platform Access ownership in one Identity database; implemented Organization backend, current mapped schema and historical design proposals. |
| [Projects module](modules/projects/README.md) | Accepted logical design (gate G3 passed 2026-11-10) for the `engagement` and `project` contexts: [ERD](modules/projects/erd.md) and [design decisions](modules/projects/design-decisions.md). Backlog, sprint and approval are boundaries only. Not implemented. |
| [ADR-001: MVP delivery capability placement](adr-001-delivery-module-placement.md) | **Accepted 2026-11-09.** New independently hosted `DeliveryPilot.Projects` module (Engagement, Project, Backlog, Sprint/Capacity, Requirements/Approval/CR) and a separate Risk module (signals, risk register, health); auth, permission, tenant, data and event rules. Authorizes planning; code and deployment changes still need reviewed tasks. |
| [Original Identity domain discovery](identity-domain-discovery.md) | Historical analysis and alternatives. Its separate Identity/Organization recommendation is superseded for the selected module; retain its reconciliation notice and provenance. |
| [Original ERD readiness analysis](identity-erd-readiness.md) | Earlier decision inventory; current scope/readiness is maintained in the module workspace. |
| [Documentation alignment audit](documentation-alignment-audit.md) | Corrections to core product/planning documents, evidence, remaining contract/release decisions and validation results. |

Within a module workspace, the README owns scope/readiness, GLOSSARY owns terminology, design-decisions owns choice/provenance, current-schema owns the source-backed physical inventory; erd/data-dictionary retain original design diagrams/column rationale, and review-scenarios owns expected cases. Keep these synchronized without treating a diagram or class file as evidence of a deployed schema or working authorization.

Identity and Projects have module design workspaces here. [ADR-001](adr-001-delivery-module-placement.md) (accepted) places the MVP delivery capabilities in a Projects module and a Risk module. The Projects workspace currently details only the `engagement` and `project` contexts; backlog, sprint, approval and the Risk module remain future design work, and no Projects business tables are implemented. Other modules access Identity through reviewed contracts, not its tables; references within the combined Identity ownership may be local foreign keys, while cross-module references remain scalar IDs.

Global policy and context loading remain in [requirements](../requirements.md), [authoring](../authoring.md) and [context map](../context-map.md).
