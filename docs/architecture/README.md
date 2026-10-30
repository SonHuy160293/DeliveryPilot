# Architecture workspace

Use this directory for scoped design evidence, selected ownership and module data models. A design choice, a proposed physical schema and implemented runtime behavior have different authority.

| Source | What it owns / how to read it |
|---|---|
| [Current backend architecture](../../ARCHITECTURE.md) | Existing independently hosted Identity, Notification and Gateway structure as documented; verify against source/tests/deployments. |
| [Target architecture proposal](../architecture.md) | Modular-monolith and technology alternatives. Selected Identity ownership does not authorize host consolidation or replace existing infrastructure. |
| [Workflow discovery](../deliverypilot-workflow-discovery.md) | Q1–Q44/W1 product workflow evidence, with derived clarifications and unresolved policy explicitly labeled. |
| [Identity & Organization module](modules/identity/README.md) | Selected combined Authentication, Organization Management and protected Platform Access ownership in one Identity database; existing authentication baseline and proposed collaboration ERD. |
| [Original Identity domain discovery](identity-domain-discovery.md) | Historical analysis and alternatives. Its separate Identity/Organization recommendation is superseded for the selected module; retain its reconciliation notice and provenance. |
| [Original ERD readiness analysis](identity-erd-readiness.md) | Earlier decision inventory; current scope/readiness is maintained in the module workspace. |
| [Documentation alignment audit](documentation-alignment-audit.md) | Corrections to core product/planning documents, evidence, remaining contract/release decisions and validation results. |

Within a module workspace, the README owns scope/readiness, GLOSSARY owns terminology, design-decisions owns choice/provenance, erd owns diagrams, data-dictionary owns column definitions, and review-scenarios owns expected cases. Keep these synchronized without treating a diagram or class file as evidence of a deployed schema or working authorization.

Only Identity currently has a module design workspace here. Project, Requirements/Approval, Delivery and other roadmap contexts remain future design work. Their placement is not decided by feature IDs or by the conceptual map alone. Other modules access Identity through reviewed contracts, not its tables; references within the combined Identity ownership may be local foreign keys, while cross-module references remain scalar IDs.

Global policy and context loading remain in [requirements](../requirements.md), [authoring](../authoring.md) and [context map](../context-map.md).
