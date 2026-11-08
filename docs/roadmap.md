# DeliveryPilot - Product and Delivery Roadmap

> **Current reconciliation — 2026-11-07:** [D1–D6](deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions) establish post-contract fixed-price delivery across all eight [lifecycle stages](sdlc-workflow.md). Release tiers below are proposals for implementation depth, not limits on the product definition. The earlier risk-first positioning is superseded. Existing IDs remain stable; 029/030 are new capability hypotheses, not approved features.

> **Status:** Proposed sequencing and feature hypotheses; no delivery dates or effort commitments approved.  
> **Owner:** Product Owner / Project Manager  
> **Related documents:** [Product](product.md) | [Requirements](requirements.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md)

**Vision repositioning — 2026-11-06:** Re-tiered for the confirmed focus: Scrum project management for software outsourcing, with AI-powered delivery risk management ([decisions V1–V5](deliverypilot-workflow-discovery.md#196-vision-repositioning-decisions)). Existing feature IDs are preserved; some names, targets and priorities changed, and FEAT-DP-025 to FEAT-DP-028 were appended. Earlier reconciliation (2026-11-03) with workflow Q1–Q44/W1 and the selected Identity scope still applies. Allocating a feature to a release does not establish delivery completion.


## 1. Roadmap objectives

Deliver a secure, traceable fixed-price outsourcing journey from signed agreement through closure. Start with an engagement-to-Sprint-Review slice, then complete lifecycle coverage and deepen engineering evidence, AI assistance and forecasting. Use vertical slices that real vendor PMs, Scrum Masters, delivery teams and client Product Owners can demonstrate and validate.

**Product release stages:**

- **MVP:** Engagement-to-Sprint-Review workflow on Scrum with scope baselines, lightweight change control, rules-based delivery risk signals, a reviewed risk register, original-versus-actual execution visibility, evidence links and scoped client visibility.
- **MVP+1 (late MVP increment):** AI risk insights: explanations, related evidence and mitigation suggestions under the guardrails.
- **V1 (proposed depth):** Per-UR proposal review, QA/UAT and release, API collaboration, hypercare/warranty/closure, Sprint/release forecasting and AI planning. This is where the complete eight-stage product journey becomes available.
- **V2:** Kanban project mode, cross-engagement resources, portfolio analytics, broader integration automation, advanced/predictive AI, commercial and enterprise capabilities, when validated.

These are **scope intentions**, not calendar commitments. P0/P1/P2 indicate roadmap priority, not incident severity. Feature-file `release` values remain `MVP|V1|V2`; MVP+1 items use `MVP` with priority `P1`.

## 2. Feature registry

The IDs below remain stable even if names or priorities change. Create feature-specific documents only when a feature enters active discovery or implementation.

| Feature ID | Name | Target | Purpose |
|---|---|---|---|
| FEAT-DP-001 | Delivery Risk Signals | MVP / P0 | Deterministic, explainable signals and candidate findings for Sprint, release and engagement risk (was Blocker Detection, V1) |
| FEAT-DP-002 | User Initialization | MVP / P0 | Registration, login, logout |
| FEAT-DP-003 | Organization & Membership | MVP / P0 | Tenant, membership, scoped collaboration foundation |
| FEAT-DP-004 | Project Initialization | MVP / P0 | Scrum project setup under an engagement, roles, stakeholders, Product Goal |
| FEAT-DP-005 | Product Discovery | V1 / P2 | Structured vision/UR discovery; MVP keeps the Product Goal and brief in FEAT-DP-004 |
| FEAT-DP-006 | Requirements Management | MVP / P1 | Versioned requirements and business rules linked to backlog items; per-UR proposal review in V1 |
| FEAT-DP-007 | Approval Workflow | MVP / P0 | Lightweight client sign-off of release/Sprint scope baselines; full reviewer gates in V1 |
| FEAT-DP-008 | Backlog Management | MVP / P0 | Product Backlog: epics, features, stories, bugs, spikes, technical work, ordering, DoR |
| FEAT-DP-009 | Scrum Delivery | MVP / P0 | Sprint Planning, board, original timebox/selection, changes, actual execution and carry-over history; Sprint close |
| FEAT-DP-010 | Delivery Risk Register | MVP / P0 | Manual and confirmed risks, issues, blockers, exposure, owners, responses (was Basic Risk & Issue Tracking) |
| FEAT-DP-011 | Team Capacity & Velocity | MVP / P0 | Sprint capacity, availability, allocation, estimation method, velocity history (was V1) |
| FEAT-DP-012 | QA Management | V1 / P1 | Test cases, test runs, defects and quality evidence |
| FEAT-DP-013 | AI Planning Assistant | V1 / P1 | Sprint selection/sequence suggestions and plan scenarios |
| FEAT-DP-014 | Portfolio Analytics | V2 / P2 | Cross-engagement health and executive views |
| FEAT-DP-015 | Client Portal | MVP / P0 | Client PO/stakeholder participation with scoped access to shared scope, reviews, health and risks |
| FEAT-DP-016 | Release Management | V1 / P1 | UAT, readiness, milestone and release evidence |
| FEAT-DP-017 | Requirement Traceability | MVP / P1 | Linked requirement, backlog, Sprint and acceptance evidence |
| FEAT-DP-018 | Sprint & Release Forecasting | V1 / P1 | Forecast/scenario layer over preserved original plan and actual execution; velocity, capacity and dependencies |
| FEAT-DP-019 | Change Request Management | MVP / P0 | Lightweight impact-assessed scope/commitment changes against baselines (was V1) |
| FEAT-DP-020 | External Integrations | V1 / P1 candidate; broader V2 | Pilot-selected engineering/test evidence adapters; manual links first; no source hosting or load engine |
| FEAT-DP-021 | Advanced AI Intelligence | V2 / P2 | Historical predictions, simulations, evaluation |
| FEAT-DP-022 | Commercial Management | V2 / P2 | Future commercial extensions if validated; initial agreement/budget/milestone evidence belongs to 025; native invoicing/accounting excluded initially |
| FEAT-DP-023 | Cross-project Resource Planning | V2 / P2 | Cross-engagement capacity and staffing conflicts |
| FEAT-DP-024 | Advanced Governance | V2 / P2 | Enterprise approval/workflow and compliance options |
| FEAT-DP-025 | Client Engagement Management | MVP / P0 | Client organization, contract type, planned budget/effort, milestones, client PO (V3) |
| FEAT-DP-026 | Scrum Events & Ceremonies | MVP / P0 | Sprint Review (client feedback/acceptance per item) and Retrospective action records |
| FEAT-DP-027 | Delivery Health Dashboard | MVP / P1 | Explainable Sprint/release/engagement health, weekly/Sprint reports and internal/client-shared views |
| FEAT-DP-028 | AI Risk Insights | MVP / P1 | MVP+1: AI explanations, evidence and mitigation drafts; subsequent report-drafting depth proposed, under human review |
| FEAT-DP-029 | API Contract & Integration Evidence | V1 / P1 proposed | Revisioned specifications, build/environment availability, consumer integration, test provenance and report export; execution delegated to external tools |
| FEAT-DP-030 | Warranty, Handover & Closure | V1 / P1 proposed | Coverage obligations, defect triage, hypercare exit, handover checklist and distinct acceptance/commercial closure evidence |

**Kanban:** Kanban project mode (Q8/Q40 behavior) is deferred to V2 by decision V1. It is not assigned a new ID yet; register one when it enters discovery rather than reusing FEAT-DP-009.

**2026-11-01 renumbering:** User Initialization was inserted as FEAT-DP-002 at the user's request. Previous features FEAT-DP-002 through FEAT-DP-023 moved to FEAT-DP-003 through FEAT-DP-024, including Organization artifact IDs, filenames and references. These IDs remain stable after that explicit renumbering. The 2026-11-06 repositioning renamed FEAT-DP-001/009/010/011/018 without renumbering.

**Active feature contracts:** [FEAT-DP-002 - User Initialization](features/FEAT-DP-002-user-initialization/feature.md) and [FEAT-DP-003 - Organization & Membership](features/FEAT-DP-003-organization/feature.md). The repositioning does not change their scope or status. Other feature files are intentionally not generated as empty placeholders.

Feature IDs identify product capabilities, not code modules or databases. FEAT-DP-002 and FEAT-DP-003 fit the selected combined [Identity module](architecture/modules/identity/README.md). Engagement, Project, Requirements/Approval, Scrum Delivery and Risk remain outside Identity; their module placement needs explicit architecture decisions.

The [workflow-to-feature mapping](deliverypilot-workflow-discovery.md#161-existing-feature-registry-mapping) routes confirmed behavior to these IDs. Registration/login/session foundations and the bounded Organization backend are implemented in the checkout; frontend and Project/guest-resource handoffs remain separate. See the [module implementation note](architecture/modules/identity/README.md#verified-implementation-baseline).

### Workflow coverage and release decisions

| Target-workflow need | Registry allocation | Remaining release decision / gate |
|---|---|---|
| Workspace, engagement and project access | FEAT-DP-002/003/004/025 in MVP | Engagement fields per contract type; project-to-engagement rules; guest admission. |
| Backlog, requirements links, scope sign-off, traceability | FEAT-DP-006/007/008/015/017 in MVP | Sprint-level vs. release-level sign-off; per-UR review stays V1 (V2 decision). |
| Sprint capacity, velocity and planning | FEAT-DP-009/011 in MVP; forecasting FEAT-DP-018 in V1 | Minimum capacity inputs; no forecast claims without history. |
| Material changes to baselines/commitments | FEAT-DP-019 (lightweight) in MVP | Minimum CR fields and decision authority for pilots. |
| Sprint Review acceptance, Retrospective | FEAT-DP-026 in MVP | Client acceptance authority per engagement. |
| Distinct DoD, QA, UAT and release evidence | DoD in FEAT-DP-009; FEAT-DP-012/016 in V1 | Lightweight outcome capture in MVP; label absent gates honestly. |
| Delivery risk signals, register, health | FEAT-DP-001/010/027 in MVP | Initial signal set and thresholds (BR-052); client-sharing defaults. |
| AI risk explanation and mitigation | FEAT-DP-028 MVP+1; FEAT-DP-013/021 later | NFR-013 evaluation and cost budget before pilot exposure. |

Hybrid mode, multiple independently scheduled teams per project, proposal branching, advanced approval chains/quorum, comprehensive test suites and automated deployment integrations remain deferred (workflow §18).

### Eight-stage coverage and capability boundaries

| Stage / cross-cutting need | Existing feature allocation | Proposed depth / unresolved work |
|---|---|---|
| LC01 Initialization | FEAT-DP-003/004/025 | Signed-contract inputs, charter, roles, communications and kickoff |
| LC02 Requirements validation | FEAT-DP-006/007/008/017/019 | Initial baseline/sign-off first; detailed optional dual-side review later |
| LC03 Design and delivery planning | FEAT-DP-008/011/013/018 | Design evidence links first; plan scenarios later |
| LC04 Scrum execution and real situation | FEAT-DP-009/010/011/026/027 | Original-versus-actual view in MVP; forecasts in 018, not timebox edits |
| LC05 System testing/UAT | FEAT-DP-007/012/016 | Named acceptance authority, defect/CR routing, evidence per version/build |
| LC06 Release | FEAT-DP-016/020 | Readiness, deployment and operational acceptance evidence |
| LC07 Hypercare/warranty | FEAT-DP-030 with 012/016 | New registry allocation: time-bound coverage and accountable obligations |
| LC08 Closure/handover | FEAT-DP-030 with 025/007 | New registry allocation: handover and separate financial/final acceptance facts |
| API specifications and consumer integration | FEAT-DP-029 with 012/017/020 | New registry allocation: domain-specific collaboration, not a test engine |
| Permission-aware search | FEAT-DP-017 with owning capabilities and 015 | Shared discovery requirement; no duplicate feature ID allocated |
| Reports and AI assistance | FEAT-DP-027/028/013 | Manual/evidence-based reporting first; AI report drafts proposed under 028; human-controlled planning under 013 |

029 and 030 fill distinct ownership gaps; actual execution belongs to existing Scrum Delivery, not a third new feature. These allocations are product proposals only. No detailed feature folders, UR/FR/US approval changes or implementation tasks are created by this refactor. Release selection of richer API/test export and AI reporting remains subject to discovery.

## 3. MVP vertical slices

A vertical slice includes user-visible behavior, permissions, persistent records and acceptance evidence, not merely completion of a backend module.

| Slice | End-to-end outcome | Primary feature IDs | Evidence gate |
|---|---|---|---|
| VS-00 | User registers, logs in and logs out | 002 | Registration, authenticated access and logout scenarios pass |
| VS-01 | Registered user creates a vendor workspace with its initial Owner and manages members | 003 | Atomic workspace/Owner establishment; isolation and membership scenarios pass |
| VS-02 | Authorized user records a Client Engagement and creates a Scrum project under it, inviting the client PO as a scoped guest | 025, 004, 015 | Contract type recorded; guest sees only that engagement's shared resources |
| VS-03 | Team builds and orders the Product Backlog with acceptance criteria and requirement links; client PO signs off a scope baseline | 008, 006, 007, 017 | Sign-off binds to exact versions; later edits stay unapproved; baseline is explicit |
| VS-04 | Team plans a Sprint and compares its preserved original plan with actual execution, capacity and blockers | 009, 011, 010 | Fixed original dates; unfinished work and later completion remain visible; no duplicate delivered points; board moves do not imply acceptance |
| VS-05 | Rules-based risk signals raise candidate findings; PM confirms risks with owner and response | 001, 010 | Signals cite evidence; dismiss/confirm attributable; works without AI provider |
| VS-06 | Client proposes a scope change; team assesses impact and records an authorized CR decision | 019, 007 | Baseline not silently replaced; rejected CR keeps current baseline |
| VS-07 | Sprint Review records client acceptance per item; Retrospective records actions; Sprint closes | 026, 009 | Acceptance distinct from DoD/release; carry-over recorded; velocity updated |
| VS-08 | Internal and client-shared delivery health views explain Sprint/release status | 027, 015, 017 | Health decomposes into signals and risks; client sees only shared items |
| VS-09 (MVP+1) | AI explains a finding and drafts mitigations for human decision | 028 | Evidence cited; no autonomous change; NFR-013 budget met |

Each slice may span several bounded contexts. Do not delay integration until every module is implemented in isolation.

## 4. Product implementation phases and exit gates

These phases organize building DeliveryPilot. They are not the eight customer-project lifecycle stages.

| Phase | Intended work | Required exit evidence |
|---|---|---|
| 00 - Product validation | Interviews with outsourcing vendors and client POs; risk-pain validation; competitor analysis; prototypes | Target delivery-risk problem and pilot journey validated |
| 01 - Foundation | Identity, tenant/project policies, invitations, observability, CI/CD | Tenant isolation and scoped access tested |
| 02 - Engagement and backlog | Client Engagement, Scrum project, Product Backlog, requirements links, scope sign-off | Client PO can sign off the correct baseline with scoped permissions |
| 03 - Scrum delivery | Capacity/velocity, Sprint Planning, board, Sprint changes, Review, Retrospective | One Sprint delivered and reviewed inside the platform |
| 04 - Delivery risk management | Risk signals, risk register, CR, delivery health, client-shared views | Signals detect seeded pilot risks with evidence; humans review every finding |
| 05 - MVP pilot and hardening | E2E tests, backups, security, performance, onboarding | Real pilot engagement actively used with measured feedback |
| 06 - AI risk insights | Authorized retrieval, structured explanations, mitigation drafts, evaluation | Findings measurable, permission-scoped and human-controlled; cost within budget |
| 07 - Lifecycle completion and planning | Forecasting, QA/UAT, release, API evidence, warranty/closure, per-UR review | PM compares original/actual/forecast; client follows one release through UAT, warranty and closure |
| 08 - Expansion | Kanban mode, portfolio, broader connector coverage, predictive AI, commercial extensions | Features justified by validated customer demand |

### Phase 00 - Product validation

- Observe how vendors report status, detect slippage and handle scope change with clients today.
- Interview delivery managers, PMs, Scrum Masters, account managers and client POs.
- Validate which risk signals matter, acceptable false-positive levels and what vendors will share with clients.
- Test the engagement-to-Sprint-Review journey with clickable prototypes.

**Gate:** Evidence shows a significant delivery-risk problem and a vendor willing to pilot.

### Phase 01 - Foundation

- Establish registration, login and logout before organization onboarding.
- Establish the combined Identity scope; implement project resource authorization in its owning capability.
- Implement workspace invitations and project/guest admission as separate scoped capabilities, with audit evidence.
- Set up automated testing, migrations, safe deployment and monitoring.

**Gate:** Isolation, membership revocation and unauthorized API scenarios pass before exposing customer data.

### Phase 02 - Engagement and backlog

- Record client engagements and contract types; create Scrum projects under them.
- Support the backlog taxonomy, acceptance criteria and requirement links.
- Support exact-version client sign-off and explicit scope baselines.

**Gate:** A client PO can sign off a scope baseline; later edits remain unapproved until signed off again.

### Phase 03 - Scrum delivery

- Support declared capacity, estimation method and velocity history.
- Support Sprint Planning, Sprint Goals, board execution and recorded Sprint changes.
- Support Sprint Review acceptance per item and Retrospective actions.

**Gate:** A pilot team can plan, execute, review and close one full Sprint.

### Phase 04 - Delivery risk management

- Start with the BR-052 signal families and configurable thresholds per contract type.
- Provide the risk register, finding review, CR linkage and delivery health views.
- Share selected health and risk items with the client explicitly.

**Gate:** Every finding has evidence and a human decision; detection works without an AI provider.

### Phase 05 - MVP pilot and hardening

- Validate end-to-end journeys and onboarding with a real engagement.
- Run negative authorization, concurrency, backup/restore and performance tests.
- Capture risk-detection lead time, false positives and user feedback.

**Gate:** Pilot engagement uses the core journey with no known critical isolation, approval-integrity or client-visibility defect.

### Phase 06 - AI risk insights

- Add authorized context retrieval, structured AI explanations and mitigation drafts.
- Establish evaluation datasets, cost budgets and feedback capture (NFR-013).

**Gate:** Findings include evidence and human decision records; no AI action bypasses approvals or tenant scope.

### Phase 07 - Planning, quality and release

- Add Sprint/release forecasting with uncertainty.
- Add test evidence, defects, UAT, release and per-UR proposal review.
- Add API/consumer evidence and warranty/handover/closure; choose external evidence connectors from pilot needs.

**Gate:** The system explains why a forecast differs from the baseline and exposes the associated quality state.

### Phase 08 - Expansion

- Expand to Kanban mode, portfolio, resource planning and broader connector coverage according to adoption evidence; pilot-selected evidence adapters are already V1 candidates.
- Evaluate probabilistic predictions only when historical data supports calibration.

**Gate:** Every expansion initiative is linked to a measurable customer outcome.

## 5. Dependencies and critical sequencing

- FEAT-DP-002 provides registration, login and logout before authenticated organization setup in FEAT-DP-003.
- FEAT-DP-003 supplies workspace ownership and access facts. FEAT-DP-025 depends on FEAT-DP-003; FEAT-DP-004 projects reference an engagement where applicable.
- FEAT-DP-015 depends on resource sharing policies, not on a `Client` role alone.
- FEAT-DP-008 and FEAT-DP-006 precede FEAT-DP-007 sign-off, which must bind to exact versions.
- FEAT-DP-011 supplies capacity and velocity to FEAT-DP-009 planning and to FEAT-DP-001 signals.
- FEAT-DP-001 needs work, Sprint, approval and engagement events; FEAT-DP-010 holds its confirmed outcomes; FEAT-DP-027 reads both.
- FEAT-DP-019 depends on baselines from FEAT-DP-007 and links to risks in FEAT-DP-010.
- FEAT-DP-028 depends on FEAT-DP-001/010 and the AI guardrails; FEAT-DP-018 needs velocity history from FEAT-DP-011.
- FEAT-DP-016 depends on verification and release-state distinctions, not merely a `Done` story status.
- Deliver integrated examples instead of treating the phase table as mandatory waterfall gates.

## 6. Estimation and calendar policy

No team composition, velocity, committed date or work-hour estimate was supplied for this project. Do **not** invent a release date from this roadmap.

Before estimating a milestone:

1. Refine feature scope and acceptance criteria.
2. Identify required roles and expertise.
3. Estimate work with ranges and explicit uncertainty.
4. Include nonfunctional, test, documentation, onboarding and operational work.
5. Calculate actual capacity including cross-project allocations and leave.
6. Map dependencies and external approval wait times.
7. Compare scenarios and record a baseline only after appropriate approval.

Do not assume Scrum must use two-week Sprints; choose cadence with the delivery team (maximum one month, BR-028).

## 7. Minimum MVP acceptance checklist

- [ ] A user can register, log in and log out.
- [ ] Organization A cannot access Organization B's resources (including background processing, risk signals and attachments).
- [ ] A user can hold organization memberships with independently scoped permissions.
- [ ] Workspace creation is separate from registration and establishes its initial Owner.
- [ ] A Client Engagement with contract type can be recorded, and a Scrum project created under it.
- [ ] A client PO guest sees only their engagement's shared scope, reviews, health and risks.
- [ ] Client sign-off of baseline v1 does not approve v2; baseline establishment is explicit.
- [ ] A team can plan, execute, review and close a Sprint with a Sprint Goal and declared capacity.
- [ ] Original dates/selection remain visible beside actual blockers and later completion; carry-over does not duplicate delivered credit.
- [ ] Sprint Review acceptance is recorded per item and stays distinct from DoD and release.
- [ ] A material scope change goes through a recorded CR decision; rejection keeps the baseline.
- [ ] Each MVP risk signal raises evidence-backed findings that a PM can confirm or dismiss, without an AI provider.
- [ ] Delivery health views decompose into signals and reviewed risks; no individual scoring.
- [ ] Risks/blockers and important project changes are visible and audited.
- [ ] Pilot onboarding and backup/recovery procedures have been verified.
- [ ] A pilot vendor validates that the risk detection is meaningfully useful.

## 8. Roadmap ownership and change rules

- PO owns product priority and outcome hypotheses, informed by stakeholder evidence.
- PM coordinates delivery forecasting, dependencies and commitments with the team.
- CTO owns architecture trade-offs and the quality/security feasibility of plans.
- User stories and estimates are refined collaboratively with delivery roles.
- A change to priority is a roadmap update; a change to an approved scope or milestone may require a separate change request.
- Completed features and evidence live in implementation/test systems, not in this hand-maintained roadmap.

## 9. Next decision

**Next:** Validate the agreement-to-review journey and the eight-stage lifecycle with vendor/client participants using the [interview guide](research/analysis/08-assumptions-and-interviews.md). Refine FEAT-DP-025/004 and existing Scrum Delivery/Forecasting boundaries, then the proposed API and warranty/closure capabilities. FEAT-DP-002/003 contracts and task readiness remain unchanged; coding requires its own task authorization and current readiness checks. The [alignment audit](architecture/documentation-alignment-audit.md) tracks remaining handoffs.
