# DeliveryPilot - Product Blueprint

> **Version:** 1.1 (working baseline; reconciled 2026-11-03)
>
> **Status:** Product direction incorporates user-confirmed workflow and selected Identity design; detailed artifact approval and customer validation remain separate.
>
> **Product type:** Multi-tenant B2B SaaS  
> **Primary market hypothesis:** Software outsourcing agencies and engineering delivery teams  
> **Document owner:** Product Owner  
> **Related documents:** [Requirements](requirements.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md) | [Roadmap](roadmap.md)

Workflow Q1–Q44 and W1 supply recorded product-owner direction. The [Identity decision log](architecture/modules/identity/design-decisions.md) records subsequent module/access choices. Derived workflow clarifications and physical schema proposals retain their own status; neither source establishes that a feature is implemented. See the [alignment audit](architecture/documentation-alignment-audit.md) for remaining handoffs.

## 1. Product definition

**DeliveryPilot is an AI-powered Software Delivery Lifecycle Management Platform.** It connects business intent and client commitments to planning, implementation, quality verification, acceptance, release, and continuous improvement. It is not only a task tracker or AI chat interface.

**North star:** *Every software delivery commitment should be understandable, traceable, measurable, and proactively manageable.*

**Value proposition:** From business intent to verified delivery, with end-to-end traceability, configurable governance, and evidence-based delivery intelligence.

DeliveryPilot should answer:

- What problem is this project solving, and what scope was agreed?
- Which requirement version was approved, by whom, and when?
- Which features, stories, implementation work, and tests satisfy a requirement?
- What has changed since the scope or delivery-plan baseline?
- Are capacity, dependencies, risks, and quality indicators threatening delivery?
- What has been developed, tested, accepted, and released?
- What action is recommended next, and what evidence supports it?

## 2. Problem and opportunity

Software teams commonly manage discovery, sign-off, engineering work, testing, and reporting in separate systems. For outsourcing agencies this makes it difficult to reconstruct client decisions, identify scope creep, coordinate approvals, or explain delivery delays. DeliveryPilot aims to reduce this fragmentation.

The initial differentiation to validate with customers is **client collaboration + requirement/version baselines + requirement-to-release traceability + explainable delivery risk assistance**. A backlog board alone is not the differentiator.

## 3. Customers, buyers, and users

**Initial ICP hypothesis (not validated):** Software agencies with roughly 10-100 staff, multiple concurrent projects, and recurring external-client approvals.

The same workspace model supports a solo practitioner and a team. Solo use is a confirmed workflow capability (Q1/Q10), not a change to the unvalidated primary-market hypothesis.

| Persona | Main jobs to be done |
|---|---|
| Executive / CTO | See portfolio health, strategic delivery risks, and decisions needing attention |
| Delivery Manager | Coordinate commitments, escalations, and shared resources |
| Project Manager | Maintain plans, milestones, dependencies, risks, and stakeholder communication |
| Product Owner | Own goals, priorities, scope decisions, and backlog value |
| Business Analyst | Discover needs, refine requirements, clarify rules, and maintain traceability |
| Scrum Master | Facilitate Scrum and help address team impediments |
| Tech Lead / Architect | Evaluate feasibility, dependencies, system design, and technical risk |
| Developer | Receive clear work, resolve dependencies, and show implementation progress |
| QA / Tester | Verify acceptance criteria, manage defects, and record evidence |
| DevOps / SRE | Plan infrastructure, reliability, and release work |
| Client stakeholder | Review shared scope and requirements, approve authorized artifacts, give UAT feedback |

A person's job title is **not** an authorization role. Organization membership, project participation, explicit permissions, resource visibility and decision assignments determine access. A System Administrator has separately controlled platform authority; that assignment does not grant customer-content access or business approval authority.

## 4. Organization, product, and project model

- **Workspace** is the user-facing term for **Organization**; **tenant** describes its ownership and isolation boundary. Personal and team workspaces use the same model.
- Registration establishes a global User identity. Workspace creation is a separate action available to a registered person, who becomes its initial Owner.
- One organization can own many projects; a user account may belong to zero or multiple organizations. Multiple Owners are allowed with last-owner protection in normal administration.
- In the MVP, each project has exactly one owning tenant.
- An external **client organization** is a business relationship, not automatically a co-owner of the vendor's project data.
- Workspace membership does not grant project access automatically. Project roles and invitations have separate authority; restricted guests may access selected projects/resources without general organization membership.
- Client stakeholders receive intentionally scoped access through the Client Portal. Visibility, required reviewer assignment and permission to approve are separate facts.
- A **product** is the evolving software outcome; a **project** is a bounded delivery initiative. MVP may store product vision within the project while leaving room for a separate Product entity later.
- Initial projects select Scrum or Kanban. Hybrid execution and several independently scheduled teams per project are deferred (Q8/Q25). Initial Scrum uses one delivery team/calendar and at most one active Sprint; Kanban requires no Sprint.

**Example:** Agency A owns an ERP implementation project. Client B can review designated requirements and UAT artifacts without accessing Agency A's internal estimates, confidential notes, or other projects.

## 5. Product capabilities across the lifecycle

These are capabilities, **not mandatory waterfall gates**. Discovery, refinement, engineering, acceptance, and replanning may overlap or repeat.

| Capability | Core activities | Representative outputs |
|---|---|---|
| Initiation and discovery | Problem definition, stakeholders, vision, goals, scope, MVP hypothesis, constraints | Project brief, vision, scope, assumptions |
| Requirements engineering | User/business/functional/NFRs, rules, revisions, questions, reviews | Requirement versions, rules, review decisions |
| Product and delivery backlog | Epics, features, stories, bugs, technical work, priorities, relations | Prioritized and traceable backlog |
| Technical and delivery planning | Feasibility, architecture, estimates, availability, allocations, dependencies | Decisions, plans, forecasts, milestones |
| Iterative delivery | Scrum Sprints or Kanban flow, assignments, blockers, work execution | Working increment, activity and progress evidence |
| Verification and acceptance | Tests, defects, NFR checks, client feedback, UAT, sign-off | Quality and acceptance evidence |
| Release and operations | Release readiness, deployments, incidents, feedback, changes | Release records, follow-up work, improvement actions |

**Key distinction:** A story being marked Done is not the same as QA Passed, Client Accepted, or Released.

## 6. Critical product concepts

| Concept | Meaning / boundary |
|---|---|
| Business objective | Outcome and value to be achieved |
| User requirement | A stakeholder or user's need |
| Functional requirement | Required system behavior |
| Non-functional requirement | Required quality, reliability, security, etc. |
| Business rule | Domain policy or decision rule |
| Reviewer approval / final acceptance | Revision-specific reviewer decision / separate authorized action making reviewed content current after applicable gates |
| Scope baseline | Explicit selection of agreed artifact versions; acceptance alone does not establish it |
| Delivery-plan baseline | Separate agreed reference plan; forecasts and draft scenarios do not replace it |
| Change request | Controlled proposal to change a baseline or commitment |
| Epic / feature / user story | Backlog decomposition to deliver valuable capabilities |
| Technical work item | Infrastructure, security, refactoring, spike or other work not necessarily under a story |
| Estimate / capacity / forecast | Distinct concepts: expected effort, available work capacity, predicted delivery outcome |
| Risk / issue / blocker / dependency | Potential event / occurred problem / active obstruction / work relationship |
| Approval | An authorized decision on a particular artifact version |
| Release | Recorded delivery of software, distinct from iteration completion |

The canonical cross-feature constraints are in [requirements.md](requirements.md). A requirement can be satisfied by multiple backlog items and vice versa. Runtime Stories may link directly to URs; FR/NFR refinement is used where applicable. This does not change repository frontmatter conventions.

## 7. Core user journeys

### Journey A - Requirement to delivery

1. A registered person creates a workspace, or joins through authorized access, then creates/accesses a project under its own permissions.
2. PM/PO/BA records purpose, scope, stakeholders and assumptions; user needs and candidate Features evolve together during discovery.
3. Contributors propose one UR change at a time per UR and submit immutable review revisions, including the relevant Feature mapping.
4. Required internal review and the selected client mode apply to the exact revision. An authorized person explicitly accepts it after the gates; scope-baseline establishment is a separate action.
5. The team refines linked Stories/AC and applicable FR/NFR/rules, prepares test scenarios, and checks readiness. A fixed Epic-to-Story hierarchy is not mandatory.
6. The team estimates and checks dependencies/declared capacity, compares scenarios, explicitly applies a reviewed plan and separately establishes any plan baseline. AI assistance is optional.
7. Scrum selection or Kanban pull rechecks readiness; Dev/QA work, verification, applicable UAT and release retain distinct outcomes and evidence. Controlled changes return affected scope/specifications/plans to the relevant gates.

This summarizes the confirmed target journey, not a declaration that every step belongs to the first release. The [workflow](deliverypilot-workflow-discovery.md#3-complete-workflow) retains the detailed loops; [roadmap scope gaps](roadmap.md#workflow-coverage-and-release-decisions) identify unresolved release allocation.

### Journey B - Client scope change

1. Client or team proposes a change to baselined scope or a delivery commitment, including agreed Story criteria even if the UR text is unchanged.
2. New proposed content remains separate from the current agreement and its baselines.
3. Team assesses dependent features, effort, timeline, risk, and cost if applicable.
4. Authorized decision-makers approve, reject, or defer the change. Approval does not itself accept revised specifications or apply a new plan.
5. Applicable review, readiness and feasibility checks govern explicit backlog/plan/baseline reconciliation. Rejected or deferred proposals retain the current baselines; routine changes within the agreement remain lightweight.

### Journey C - Proactive delivery intervention

1. Dependencies, blocked work, planned effort, availability, and milestones generate delivery signals.
2. Rules detect objective concerns before optional AI explanation.
3. A finding records evidence and affected artifacts.
4. PM reviews suggested options; no automatic commitment change occurs.
5. The team records action and outcome for later improvement.

## 8. MVP and release strategy

**MVP goal:** An agency can demonstrate the core requirement-to-work and client-review path of Journey A in a secure, auditable workspace, execute Scrum or Kanban work, and register a blocker. Journey A now describes the broader confirmed target workflow. The minimum planning, change-control and verification/release evidence needed for the pilot still needs explicit release allocation; do not claim full journey coverage from the current registry. See [roadmap](roadmap.md).

**MVP:** Organization/membership; project and discovery; requirements and versioning; configurable basic approval; backlog; core Scrum/Kanban delivery; basic risks/issues; scoped Client Portal; traceability.

**Next release (existing proposed allocation):** Capacity/estimation tools, QA/UAT/release management, formal change-request management, forecasting, rule-based blocker detection and AI assistance. Their placement does not waive version-bound review, preservation of agreed scope, or honest labeling of delivery evidence in earlier slices. Lightweight outcomes versus full management tooling need the roadmap decisions noted above.

**Later:** Cross-project resource and portfolio analysis, richer integrations, probabilistic predictions, enterprise governance, and validated commercial features.

**MVP non-goals:** Billing, project co-ownership across organizations, enterprise-grade custom workflow builders, hybrid delivery, advanced predictive AI, and automated employee scoring. Multiple Owners within one workspace remain in the selected design.

## 9. AI product principles

- Prefer explainable rules for objective conditions such as over-capacity or overdue approvals.
- Use AI for interpretation, drafting, alternative scenarios, and context-aware recommendations.
- Every actionable finding should link to authorized evidence and indicate uncertainty.
- Human approvers retain control over baselines, assignments, client communications, and delivery commitments.
- Apply tenant and user authorization before retrieving any content for an AI analysis.
- Do not claim statistically reliable forecasts without enough representative history and evaluation.
- Do not use workload signals as automated judgments of employee worth or performance.

See the [target architecture](architecture.md) for proposed technical guardrails and the [architecture workspace](architecture/README.md) for selected module design and implementation boundaries.

## 10. Success criteria and measures

**Customer outcome hypotheses:** Shorter approval turnaround; fewer unnoticed scope changes; improved ability to explain delivery status and impacts; earlier detection of material delivery risks.

Candidate product measures:

- Adoption: activated organizations, active projects, pilot-to-paid conversion.
- Requirements: approval lead time, rework rate, approved-version traceability coverage.
- Delivery: milestone predictability, blocked work age, cycle time, throughput, forecast error.
- Quality: defect escape rate, UAT pass rate, reopened work.
- AI: finding precision, false positive rate, time-to-detection, accepted recommendations, cost per analysis.

Define precise measurement methods before treating these as performance targets. Metrics must not be used to rank individuals by story points or hours.

## 11. Product principles

1. **Business value first.** Validate problems before replicating competitor features.
2. **Trace critical commitments.** Keep the why, decision, version, and resulting delivery evidence connected.
3. **Support Agile; do not force Waterfall.** Lifecycle capabilities can run concurrently or iteratively.
4. **Human decision, AI assistance.** No autonomous modifications to approved commitments.
5. **Security by design.** Tenant isolation and scoped client access are foundational.
6. **Evidence before certainty.** Expose source evidence and forecast assumptions.
7. **Ship vertical slices.** Favor complete business journeys over isolated technical modules.
8. **Design for evolution.** Clear boundaries without premature distributed complexity.

## 12. Open product hypotheses

These decisions require customer evidence and/or formal acceptance:

- Actual initial ICP, buyer, purchasing process and willingness to pay.
- Customer validation of the initial one-UR proposal workflow and its Feedback Only / Approval Required modes; any later package/milestone approval needs separate scope refinement.
- The minimum client-facing visibility necessary for a pilot.
- Initial Product/Project distinction and client engagement/contract needs.
- Billing, data residency, regulatory constraints, and integrations.
- MVP delivery team, infrastructure budget, target dates, and the lightweight planning/change/verification outcomes needed before claiming the full target journey.

**Change discipline:** Update this file when the *product direction or business rationale* changes. Put cross-feature behavior in [requirements.md](requirements.md), evidence in [workflow discovery](deliverypilot-workflow-discovery.md), module design in the [architecture workspace](architecture/README.md), topology alternatives in [architecture.md](architecture.md), and release allocation in [roadmap.md](roadmap.md). Preserve each source's authority label.
