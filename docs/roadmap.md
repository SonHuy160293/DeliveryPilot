# DeliveryPilot - Product and Delivery Roadmap

> **Status:** Proposed sequencing and feature hypotheses; no delivery dates or effort commitments approved.  
> **Owner:** Product Owner / Project Manager  
> **Related documents:** [Product](product.md) | [Requirements](requirements.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md)

Reconciled 2026-11-03 with workflow Q1–Q44/W1 and selected Identity scope. Feature IDs, release targets and priorities are unchanged. Workflow confirmation specifies intended behavior; it does not allocate every stage to MVP or establish delivery completion.

## 1. Roadmap objectives

Deliver an end-to-end, secure user journey for a software agency before expanding into detailed forecasting and predictive AI. Use vertical slices that can be demonstrated and validated by real PMs, BAs, engineering teams, and client approvers.

**Product release stages:**

- **MVP:** Client-inclusive requirement-to-delivery workflow with versioning, approvals, and traceability.
- **V1:** Deeper capacity/estimation, verification/UAT, change management, explainable blocker detection, and assistive AI.
- **V2:** Portfolio and resource optimization, integrations, advanced intelligence, and enterprise/commercial capabilities only when validated.

These are **scope intentions**, not calendar commitments. P0/P1/P2 in this document indicate roadmap priority, not incident severity.

## 2. Feature registry

The IDs below remain stable even if priorities change. Create feature-specific documents only when the feature enters active discovery or implementation.

| Feature ID | Name | Target | Purpose |
|---|---|---|---|
| FEAT-DP-001 | Blocker Detection | V1 / P1 | Proactive and explainable work obstruction signals |
| FEAT-DP-002 | User Initialization | MVP / P0 | Registration, login, logout |
| FEAT-DP-003 | Organization & Membership | MVP / P0 | Tenant, membership, scoped collaboration foundation |
| FEAT-DP-004 | Project Initialization | MVP / P0 | Project ownership, setup, roles and stakeholders |
| FEAT-DP-005 | Product Discovery | MVP / P0 | Project vision, goals, scope and MVP hypotheses |
| FEAT-DP-006 | Requirements Management | MVP / P0 | Versioned requirements, business rules and review |
| FEAT-DP-007 | Approval Workflow | MVP / P0 | Authorized artifact-version decisions and history |
| FEAT-DP-008 | Backlog Management | MVP / P0 | Epics, features, stories, work items and priorities |
| FEAT-DP-009 | Agile Delivery | MVP / P0 | Core Scrum and Kanban execution workflows |
| FEAT-DP-010 | Basic Risk & Issue Tracking | MVP / P0 | Manual risks, issues and blockers |
| FEAT-DP-011 | Team Capacity & Estimation | V1 / P1 | Allocation, availability, estimation and load |
| FEAT-DP-012 | QA Management | V1 / P1 | Test cases, test runs, defects and quality evidence |
| FEAT-DP-013 | AI Planning Assistant | V1 / P1 | Assist requirement/story drafting and plan scenarios |
| FEAT-DP-014 | Portfolio Analytics | V2 / P2 | Cross-project health and executive views |
| FEAT-DP-015 | Client Portal | MVP / P0 | External client participation with scoped access |
| FEAT-DP-016 | Release Management | V1 / P1 | UAT, readiness, milestone and release evidence |
| FEAT-DP-017 | Requirement Traceability | MVP / P0 | Linked requirement, backlog and delivery evidence |
| FEAT-DP-018 | Delivery Forecasting | V1 / P1 | Baseline vs forecast with capacity/dependency context |
| FEAT-DP-019 | Change Request Management | V1 / P1 | Controlled scope revisions and impact decisions |
| FEAT-DP-020 | External Integrations | V2 / P2 | GitHub/GitLab/Azure DevOps and messaging adapters |
| FEAT-DP-021 | Advanced AI Intelligence | V2 / P2 | Historical predictions, simulations, evaluation |
| FEAT-DP-022 | Commercial Management | V2 / P2 | Contract, budget and billing extensions if validated |
| FEAT-DP-023 | Cross-project Resource Planning | V2 / P2 | Portfolio capacity and staffing conflicts |
| FEAT-DP-024 | Advanced Governance | V2 / P2 | Enterprise approval/workflow and compliance options |

**2026-11-01 renumbering:** User Initialization was inserted as FEAT-DP-002 at the user's request. Previous features FEAT-DP-002 through FEAT-DP-023 moved to FEAT-DP-003 through FEAT-DP-024, including Organization artifact IDs, filenames and references. These IDs remain stable after this explicit renumbering.

**Active feature contracts:** [FEAT-DP-002 - User Initialization](features/FEAT-DP-002-user-initialization/feature.md) and [FEAT-DP-003 - Organization & Membership](features/FEAT-DP-003-organization/feature.md). Other feature files are intentionally not generated as empty placeholders.

Feature IDs identify product capabilities, not code modules or databases. FEAT-DP-002 and FEAT-DP-003 fit the selected combined [Identity module](architecture/modules/identity/README.md), which also contains a protected Platform Access design foundation. Project participation, Requirements and Approval remain outside Identity. No decision here merges Project with Requirements/Approval or creates a new platform feature ID. Operational platform administration needs separate scope/readiness review.

The [workflow-to-feature mapping](deliverypilot-workflow-discovery.md#161-existing-feature-registry-mapping) routes confirmed behavior to these existing IDs. Current source remains partial: registration/login/session foundations exist; organization access is not an implemented journey, and newly drafted domain classes alone do not satisfy a slice. The [module implementation note](architecture/modules/identity/README.md#verified-implementation-baseline) separates classes, mappings and schema.

### Workflow coverage and release decisions

The original MVP slices cover only part of W1's seven-stage target journey. Q41 asks for lightweight delivery outcomes before full QA/deployment suites, while the registry places related management features in V1. Keep this gap visible until the product owner allocates the minimum pilot scope; do not silently move features between releases or promise complete Journey A coverage.

| Target-workflow need | Existing registry allocation | Remaining release decision / gate |
|---|---|---|
| Workspace/project access, UR/Feature discovery, review and acceptance, backlog and explicit traceability | FEAT-DP-002–010/015/017 in MVP | Refine selected review modes, guests, readiness, separate acceptance/baseline actions and scope-specific negative tests into existing feature contracts. |
| Estimates, declared capacity/dependencies and feasible manual planning | FEAT-DP-011/018 in V1 | Choose the minimum pilot inputs and evidence; do not claim capacity-aware commitment or forecasting without them. |
| Material changes to baselines/commitments | FEAT-DP-019 in V1; version/baseline integrity in FEAT-DP-006/007 | Choose a lightweight recorded authorized path or explicitly constrain pilot changes until it exists. Deferring a management UI does not permit silent baseline replacement. |
| Distinct completion, QA, applicable UAT and release evidence | FEAT-DP-012/016 in V1; traceability in FEAT-DP-017 MVP | Allocate lightweight outcome/evidence capture versus full suites; link only evidence that exists and label absent gates honestly. |
| Scenario suggestions, explainable findings and automated evaluation | FEAT-DP-001/013/018 in V1 | Preserve manual reporting and human decisions in MVP; AI availability is not a prerequisite for them. |

These are unresolved allocation choices, not new approved acceptance scope. Hybrid mode, multiple independently scheduled project teams, proposal branching, advanced approval chains/quorum, comprehensive test suites and automated deployment integrations remain deferred in workflow §18.

## 3. MVP vertical slices

A vertical slice includes user-visible behavior, permissions, persistent records and acceptance evidence, not merely completion of a backend module.

| Slice | End-to-end outcome | Primary feature IDs | Evidence gate |
|---|---|---|---|
| VS-00 | User registers, logs in and logs out | 002 | Registration, authenticated access and logout scenarios pass |
| VS-01 | Registered user separately creates a workspace with its initial Owner, then creates/configures a project under scoped authority | 003, 004 | Atomic workspace/Owner establishment; workspace and project access remain separate |
| VS-02 | BA explores vision/UR/Feature mappings and submits an immutable review revision | 005, 006 | Draft discovery, revision history, publication scope and validation pass |
| VS-03 | Authorized reviewers complete internal and selected client gates; authorized person explicitly accepts the revision | 007, 015 | Required/optional and feedback/approval modes; solo exception preserves client gate; exact revision and denied access verified |
| VS-04 | Authorized person explicitly establishes selected scope baseline; team links/refines Features, Stories/AC and applicable specifications | 006, 007, 008, 017 | Acceptance does not imply baseline; links pin versions and respect visibility; DoR and draft scenarios remain distinct from approval/tests |
| VS-05 | Team executes Scrum selection or Kanban pull with readiness checks and manual blockers | 009, 010 | Scrum goal/selection history; Kanban without Sprint and visible WIP policy; board movement does not imply dates/acceptance/release |
| VS-06 | Stakeholders follow requirement-to-work progress and decisions | 017 plus prior slices | Traceability is reproducible and audit evidence visible |

Each slice may span several bounded contexts. Do not delay integration until every module is implemented in isolation.

## 4. Delivery phases and exit gates

| Phase | Intended work | Required exit evidence |
|---|---|---|
| 00 - Product validation | Interviews, actual workflows, competitor analysis, MVP hypothesis, UX prototypes | Target user problem and pilot journey validated |
| 01 - Foundation | Identity, tenant/project policies, invitations, observability, CI/CD | Tenant isolation and scoped access tested |
| 02 - Discovery and requirements | Project vision/scope, requirements, revisions, business rules | BA can manage accurate requirement history |
| 03 - Backlog and delivery | Epic/feature/story/work item, dependencies, Scrum/Kanban | One iteration can be delivered inside the platform |
| 04 - Client governance | Client portal, version-bound approval, baselines, traceability | Client can sign off correct version with scoped permissions |
| 05 - MVP pilot and hardening | E2E tests, backups, security, performance checks, onboarding | Real pilot project completed or actively used with measured feedback |
| 06 - Planning and release | Capacity, effort, forecast, change requests, QA/UAT, release | PM compares baselines, forecast and actual; QA/UAT evidence exists |
| 07 - AI intelligence | Rules/signals, AI drafting, evidence-backed findings and evaluation | Findings measurable, permission-scoped and human-controlled |
| 08 - Expansion | Portfolio, integrations, advanced AI, commercial extensions | Features justified by validated customer demand |

### Phase 00 - Product validation

- Observe requirement sign-off, scope change, delivery planning and UAT in real organizations.
- Interview representative PM, BA, PO, delivery leader and client stakeholder roles.
- Validate buyer, workflow pain, competing tools, budget and adoption friction.
- Test the proposed MVP journey with clickable prototypes or lightweight workflow demos.
- Record open assumptions rather than treating suggested policies as signed-off requirements.

**Gate:** Evidence shows there is a significant user problem and a customer willing to trial the MVP workflow.

### Phase 01 - Foundation

- Establish user registration, login and logout before organization onboarding.
- Establish the combined Identity scope for authentication and workspace management; implement project resource authorization in its owning capability.
- Implement workspace invitations and project/guest admission as separate scoped capabilities, with appropriate audit evidence.
- Set up automated testing, migrations, safe deployment and monitoring.

**Gate:** Isolation, membership revocation and unauthorized API scenarios pass before exposing customer data.

### Phase 02 - Discovery and requirements

- Support project vision, scope and MVP documents with revision history.
- Support requirement types, relationships, business rules, review and proposal status.
- Build the distinction between draft, reviewed and approved data into UI language.

**Gate:** A BA can draft, revise and explain a requirement without losing provenance.

### Phase 03 - Backlog and delivery

- Support the basic backlog taxonomy and story acceptance criteria.
- Support Scrum Sprint Goals / selections and Kanban pull/WIP semantics as separate modes.
- Support technical tasks and manual blockers independently of user stories.

**Gate:** A pilot team can refine, select, execute and inspect delivery work in one full iteration.

### Phase 04 - Client governance

- Enable resource-scoped client access.
- Implement independent internal/client gates, required/optional reviewers and immutable target-version decisions.
- Separate reviewer approval, final acceptance and explicit baseline selection; retain requirement-to-work links and decision history.

**Gate:** A client can complete the configured review mode for a permitted revision; acceptance respects required gates, new edits remain unapproved, and baseline establishment is explicit. Minimum pilot change handling must be resolved from the release table above.

### Phase 05 - MVP pilot and hardening

- Validate end-to-end user journeys and onboarding.
- Run negative authorization, concurrency, backup/restore and high-value performance tests.
- Capture real user feedback, defect trends and activation metrics.

**Gate:** Pilot organization uses the core journey with no known critical data isolation or approval-integrity defect.

### Phase 06 - Planning, change and quality

- Add allocation/availability, role bottlenecks and estimate methods.
- Add milestone/forecast comparisons and impact assessments.
- Add test execution, defect/UAT tracking, acceptance and release evidence.

**Gate:** The system explains why a milestone forecast differs from an approved baseline and exposes the associated quality state.

### Phase 07 - AI intelligence

- Start with deterministic risk signals: overdue approvals, blocked work age, capacity exceedance and delayed dependencies.
- Add authorized context retrieval, structured AI explanation, recommendations and feedback.
- Measure false positives and cost before exposing advanced forecasting.

**Gate:** Findings include evidence and human decision records; no AI action bypasses approvals or tenant scope.

### Phase 08 - Portfolio and expansion

- Expand analytics, resource planning and integrations according to adoption evidence.
- Evaluate probabilistic predictions only when historical data supports calibration.

**Gate:** Every expansion initiative is linked to a measurable customer outcome.

## 5. Dependencies and critical sequencing

- FEAT-DP-002 provides user registration, login and logout before authenticated organization setup in FEAT-DP-003.
- FEAT-DP-003 supplies workspace ownership and access facts. FEAT-DP-004 owns project participation and FEAT-DP-015 resource sharing; guests do not need general workspace membership.
- FEAT-DP-004 establishes project ownership and settings used by most feature contexts.
- FEAT-DP-005/006 define UR/Feature discovery and version semantics before FEAT-DP-007 review can safely bind to revisions; acceptance and baselining retain distinct actions.
- FEAT-DP-015 depends on resource sharing policies, not simply on adding a `Client` role.
- FEAT-DP-017 requires stable version-aware links across requirements/backlog and authorized visibility at both ends. Runtime direct UR-to-Story links do not change repository frontmatter; reconcile tooling separately if needed.
- FEAT-DP-011 supplies capacity inputs to forecasting; estimates alone cannot produce reliable commitments.
- FEAT-DP-001 can begin with rules once work/activity signals are available; predictive AI can come later.
- FEAT-DP-016 depends on verification and release-state distinctions, not merely a `Done` story status.
- FEAT-DP-019's full management capability can be later, but any slice exposing baselines must preserve agreement integrity and define how material pilot changes are handled.
- Confirmed workflow dependencies are independent of phase numbering; deliver integrated examples instead of treating the phase table as mandatory waterfall gates.

## 6. Estimation and calendar policy

No team composition, velocity, committed date, or real work-hour estimate was supplied for this project. Do **not** invent a release date from this roadmap.

Before estimating a milestone:

1. Refine feature scope and acceptance criteria.
2. Identify required roles and expertise.
3. Estimate work with ranges and explicit uncertainty.
4. Include nonfunctional, test, documentation, onboarding and operational work.
5. Calculate actual capacity including cross-project allocations and leave.
6. Map dependencies and external approval wait times.
7. Compare scenarios and record a baseline only after appropriate approval.

Do not assume Scrum must use two-week Sprints; choose cadence with the delivery team. Kanban planning should remain flow-based.

## 7. Minimum MVP acceptance checklist

- [ ] A user can register, log in and log out.
- [ ] Organization A cannot access Organization B's resources (including background processing and attachments).
- [ ] A user can hold organization memberships with independently scoped permissions.
- [ ] Workspace creation is separate from registration and establishes its initial Owner; selected owner/rejoin/delegation rules are covered.
- [ ] External client reviewer sees only shared artifacts and can complete an authorized approval.
- [ ] Restricted guest access works without general workspace membership; feedback-only and required-approval modes retain separate gates.
- [ ] Approval of requirement v1 does not approve v2.
- [ ] A BA can trace an approved requirement to related feature/story and associated work.
- [ ] Acceptance and explicit baseline establishment are separate; material-change handling for the pilot is defined and tested.
- [ ] A team can complete a Scrum iteration with a Sprint Goal.
- [ ] A team can complete a Kanban workflow without a Sprint.
- [ ] Risks/blockers and important project changes are visible and audited.
- [ ] Pilot onboarding and backup/recovery procedures have been verified.
- [ ] A pilot stakeholder validates that the workflow is meaningfully useful.

## 8. Roadmap ownership and change rules

- PO owns product priority and outcome hypotheses, informed by stakeholder evidence.
- PM coordinates delivery forecasting, dependencies and commitments with the team.
- CTO owns architecture trade-offs and the quality/security feasibility of plans.
- User stories and estimates are refined collaboratively with delivery roles.
- A change to priority is a roadmap update; a change to an approved scope or milestone may require a separate change request.
- Completed features and evidence live in implementation/test systems, not in this hand-maintained roadmap.

## 9. Next decision

**Next:** FEAT-DP-003 now has internally ready UR/FR/US after the joint U9–U21 review; use its resolved scope for source-aware task planning. The existing Organization task remains blocked pending technical replanning. Inspect current FEAT-DP-002 implementation/task status separately. Refine FEAT-DP-004 project/guest authority before planning the workspace-to-project slice. In parallel, allocate the lightweight planning/change/evidence outcomes in the release table and prototype the exact-revision client-review journey. The [alignment audit](architecture/documentation-alignment-audit.md) tracks remaining contract and readiness handoffs.
