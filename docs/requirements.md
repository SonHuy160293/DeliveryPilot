# DeliveryPilot - Cross-Feature Requirements and Business Rules

> **Status:** Working cross-feature contract, reviewed 2026-11-08 against current D1–D6 direction and recorded feature decisions. User-confirmed rules remain distinct from proposals; no artifact approval is conferred.
>
> **Owner:** Product Owner / Business Analyst  
> **Related documents:** [Product](product.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md) | [Roadmap](roadmap.md) | [Organization feature](features/FEAT-DP-003-organization/feature.md)

> **2026-11-07 reconciliation:** [D1–D6](deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions) update the product boundary and lifecycle. BR-058–BR-067 distinguish that direction from proposed operating rules. Existing stable IDs and approval evidence are preserved.

## 1. Purpose and authority

This file owns **cross-feature behavioral expectations, domain vocabulary, business rules, security expectations, and NFR categories**. It does not replace detailed discovery and acceptance criteria in a feature file. Candidate behavior must be validated with real stakeholders before being treated as a contractual commitment.

Use these labels:

- **Agreed direction:** Product principles accepted during planning; still subject to discovery refinement.
- **User-confirmed:** A choice recorded in workflow Q1–Q44/W1, vision decisions V1–V5, lifecycle decisions D1–D6, or the Identity design decision log. Applies only to that recorded scope; does not promote UR/FR/US statuses or imply customer validation.
- **Proposed rule:** Recommended domain policy, not yet signed off by stakeholders.
- **Open decision:** A choice requiring a project or customer decision.

Evidence is maintained in [workflow discovery](deliverypilot-workflow-discovery.md#19-discovery-decision-record) and [Identity decisions](architecture/modules/identity/design-decisions.md). Workflow A1–A4 and physical storage representations remain proposals. Stable BR/NFR identifiers below are retained; the [alignment audit](architecture/documentation-alignment-audit.md) identifies downstream contracts still needing refinement.

## 2. Domain glossary

| Term | Business meaning |
|---|---|
| Workspace / Organization / Tenant | Workspace is the user-facing name for Organization; tenant describes its ownership/isolation boundary. Solo and team use share this model. |
| User | Global identity; authentication alone supplies no workspace, project or decision authority |
| Membership / project participation | Separate scoped relationships; general workspace membership is not required for a restricted project guest |
| Owner / System Administrator | Protected workspace responsibility / independent platform authority; neither implies unrestricted customer-content access |
| Project | Bounded delivery initiative under one owning tenant in MVP |
| Product | Long-lived software solution that may span multiple projects |
| Client Organization | External customer business; does not implicitly gain ownership or access |
| Client Engagement | Vendor-owned record of the delivery relationship with one client: contract type, planned budget/effort, milestones, client Product Owner (V3) |
| Contract type | Fixed-price in initial scope; Time & Materials and Dedicated Team are future concepts. Applicable policies must identify their supported contract model. |
| Client Product Owner | Client-side person assigned to clarify priorities, sign off scope baselines and accept increments; the assignment, not a role name, grants decision authority |
| Stakeholder | Person or party with interest in, responsibility for, or authority over project outcomes |
| Requirement | Recorded business/user/functional/quality need or constraint |
| Requirement proposal / review revision | Editable proposed UR change / immutable submitted snapshot; initially one active proposal per UR |
| Requirement Version | A specific revision; submitted review content and its decisions remain immutable |
| Reviewer approval / final acceptance | Exact-revision review decision / separate authorized action making reviewed content current after all applicable gates |
| Scope baseline | Explicit selection of agreed artifact versions; does not follow automatically from acceptance or links |
| Delivery-plan baseline | Separate explicit reference plan, distinct from current execution, draft scenarios and forecasts |
| Approval | A recorded authorized decision on its exact target under applicable policy; initial UR review is distinct from a CR decision or UAT |
| DoR / DoD | Readiness for selected work / completion criteria; neither fabricates client approval, QA, UAT or release evidence |
| Change Request | Proposal to modify an established scope/commitment |
| Backlog Item | Piece of planned work representing value, defect, investigation, or technical obligation |
| Sprint | Scrum timebox with an explicit Sprint Goal and selected backlog |
| Product Goal / Sprint Goal | Long-term product objective / single-Sprint commitment |
| Increment | Work meeting the DoD in a Sprint; client acceptance is a separate recorded fact |
| Sprint Review / Retrospective | Scrum events that record increment feedback/acceptance and improvement actions |
| Velocity | Completed estimate per Sprint for one team, used only as a planning and risk input |
| Kanban Flow | Pull-oriented workflow with explicit work policies and WIP management (project mode deferred to release V2 by decision V1) |
| Estimate | Expected amount/size of work, with uncertainty |
| Capacity | Resource time actually available in a planning interval |
| Forecast | Predicted timing/outcome based on scope, dependencies, data and assumptions |
| Risk | Potential future event affecting outcomes |
| Issue | Problem that has already occurred |
| Blocker | Obstacle preventing work from progressing |
| Dependency | Relationship between work or deliverables; may be healthy or problematic |
| Risk signal | Deterministic, configurable detection of an objective condition with defined inputs |
| Candidate finding | Evidence-backed concern from a signal or AI analysis awaiting human review |
| Risk exposure | Reviewed probability and impact rating of a confirmed risk, with owner and response |
| Delivery health | Explainable summary for a Sprint, release or engagement derived from signals and reviewed risks; never an individual score |

Additional shared vocabulary:

| Term | Business meaning |
|---|---|
| Lifecycle stage | Governance/activity view over project or release scope; not a mandatory exclusive project status |
| Original Sprint snapshot | Preserved start/end dates and originally selected work; later scope changes have separate records |
| Execution timeline | Actual work and blocked intervals with as-of time; forecasts/scenarios are separately labelled |
| API contract revision | Version of interface specification, separate from its availability in a deployed build/environment |
| Consumer integration | Consumer-specific compatibility/integration evidence against an API revision and build |
| Test run evidence | Results tied to specification revision, build, environment, scenario/configuration and execution time |
| Warranty / closure | Coverage obligations / explicit completion and handover decisions; release alone establishes neither |

## 3. Identity, ownership, and collaboration

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-001 | Each MVP project has exactly one owning tenant. | Agreed direction |
| BR-002 | One user identity may have memberships in multiple organizations. | Agreed direction |
| BR-003 | Membership in an organization does not automatically grant project access; restricted guests may receive explicit project/resource access without organization membership. | User-confirmed: Q2/Q3 |
| BR-004 | External clients may only access resources explicitly authorized for their role and project. | Agreed direction |
| BR-005 | Job titles and project roles are separate from effective permissions. | Agreed direction |
| BR-006 | A user must not grant powers outside their delegation authority. Ordinary organization role managers need role-management authority, must hold the granted permissions, and may delegate only permitted organization powers. Protected Owner/platform operations have separate controls. | User-confirmed: Q5; AUTHZ-03/05 |
| BR-007 | Removing a project member must revoke future access without deleting historical approval/audit attribution. | Proposed rule |
| BR-008 | Users and automation must not infer authorization from a caller-provided tenant/organization ID alone. | Agreed direction |

### Selected Identity and collaboration direction

The following summarizes user-selected rules; detailed evidence and still-open policy belong to the [Identity decision log](architecture/modules/identity/design-decisions.md). These are intended behaviors, not implemented access controls.

| Area | Selected direction and limit | Evidence |
|---|---|---|
| Workspace creation | Separate from registration; a registered creator becomes initial Owner. Only a trimmed nonblank name up to 160 characters is required; duplicate names allowed, no slug. Multiple Owners and normal last-owner protection apply. An existing Owner nominates an existing member who must accept additional ownership; transfer is add/accept then separate step-down, preserving other roles or Member fallback. Seven-day in-app nomination and current sponsor/nominee checks apply; emergency recovery is separately scoped. | Q1; CREATE-01; OWN-01/05–08; U9/U12/U15/U19 |
| Membership | One durable User/Organization membership with history. Removal/rejoin requires newly authorized roles; suspension/restoration commands/UI are deferred; suspended joining/access denied. Active recipients use role management; removed recipients use fresh-authorized rejoin. Original join time persists. | MEM-01–05; U14/U18 |
| Organization roles | Multiple roles combine allowed permissions within the workspace, subject to mandatory restrictions. Protected Owner/Admin/Member definitions coexist with custom roles; U11–U17 select default capability matrix, protected targets, removal ceiling, custom-role inputs/retirement and stale-edit/upgrade policy. Code-name mapping is task design. | AUTHZ-01–12; U11–U17 |
| Invitations | One pending offer per organization/normalized email. Redemption needs a valid single-use link plus matching current signed-in email; changed offered permissions require reissue. No separate fresh email code or silent global email-verification update is implied. Seven-day expiry; rotated resend invalidates old proof and restarts seven days. Acceptance revalidates current inviter authority; authority loss requires current authorized reissue. U14/U16/U21 select duplicate-issue conflicts, proof-protected preview/decline, pending cancellation, fresh Issue for closed offers, first-valid-transition/replay and delivery retries without deadline extension. Shared configurable issuance limits: 60-second recipient cooldown, 10 recipient-pair issuances/24h, 100 workspace issuances/hour. | INV-01–11; AUTHZ-04; U10/U14/U16/U21 |
| Workspace member/audit visibility and removal | Owners/Admins plus explicitly delegated readers may inspect member lists/current roles and membership/role history through distinct grants; each member may inspect their own membership/roles. Owners/Admins plus delegated membership managers are eligible to remove ordinary non-Owner members. Only Owners remove Admin memberships; Owner reductions are Owner-controlled and preserve an active Owner. Member fields are display name, primary email, membership state, current workspace roles and join time. U13 restricts Admin role reductions to Owners and applies ordinary-removal target-power ceiling. Join time is original creation time; active list default with explicit historical filters. | AUTHZ-07–10; DATA-04/05; U11–U13/U18 |
| Platform authority | Common User identity with separate system-controlled assignments, independent of organization membership. Organization administration cannot grant platform authority or customer-content bypass. Privileged activation, bootstrap, MFA/recovery and operational procedures remain deferred. | SCOPE-02/03; PLAT-01–03 |
| Project creation/access | Owner/Admin or delegated Create Project can create; the creator initially administers that project. Project roles, guest admission, sharing and review authority remain resource-owned. Organization role-combination choices do not settle project-role policy. | Q2/Q3/Q5/Q6; SCOPE-04 |

Identity combines Authentication, Organization Management and protected Platform Access in one module-owned database. That selected design permits local identity/organization references, but does not consolidate current hosts or place Project/Requirements/Approval inside Identity. Code-defined permissions, stable grant rows and shared audit history are design choices documented in the module workspace; their schema and enforcement must not be inferred from this summary.

**Use case for validation:** Agency A invites a user from Client B to approve one requirement. The user can inspect that requirement and the explicitly shared context, but cannot retrieve unrelated requirements, internal capacity reports, or another client's project by guessing IDs.

## 4. Requirement authoring, revisioning, and traceability

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-009 | Requirements are separate domain artifacts from backlog work items. | Agreed direction |
| BR-010 | Requirements can link many-to-many with Features and Stories; runtime direct UR-to-Story links are allowed and FR/NFR refinement is used where applicable. Links do not approve their targets. | User-confirmed: Q17/Q19/W1 |
| BR-011 | Approved requirements refer to a particular revision or immutable version. | Agreed direction |
| BR-012 | Editing content after approval creates a new proposed version; old approval evidence remains bound to the old version. | Agreed direction |
| BR-013 | Scope and delivery-plan baselines are separate explicit, reproducible selections. Accepting content or applying a plan does not automatically establish or replace either baseline. | User-confirmed: Q18/Q39 |
| BR-014 | Relationships between requirement, implementation, verification and release artifacts should remain navigable even when one is retired. | Proposed rule |
| BR-015 | Assumptions and unresolved questions must not be displayed as approved requirements. | Agreed direction |

### Required requirement attributes (candidate)

- Stable identifier; tenant/project; classification; title; description; author/owner.
- Revision and lifecycle status; rationale; source/stakeholder; related assumptions.
- Business rules, NFR applicability and traceability links (where relevant).
- Approval/baseline reference when applicable.

### Proposal, review and acceptance

Workflow Q4/Q9–Q17 confirms one active proposal per UR, collaborative authorship and protection against silent overwrites. Submission freezes a review revision. Changed content creates a new revision; comments and decisions remain attached to their original revision. A client continues seeing the deliberately published revision until another is explicitly shared.

Reviewer approval, final acceptance and baseline establishment are separate actions. Do not implement a single `Approved -> Baselined` transition as though those facts were automatic. Exact runtime statuses remain design work; the [workflow representation](deliverypilot-workflow-discovery.md#64-lifecycle-representation) is illustrative.

W1 adds iterative UR/Feature discovery before review and parallel Story/AC plus applicable FR/NFR/rule refinement afterward. The reviewed mapping must remain explainable; snapshot/version representation is proposed guidance A1. Work pins the requirement versions it implements; later revisions trigger impact review rather than automatic work or plan edits (Q21). Repository frontmatter still follows the [authoring policy](authoring.md#repository-artifacts-versus-product-runtime).

## 5. Approval and change governance

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-016 | Approval is applied only when an applicable policy requires it. | Agreed direction |
| BR-017 | A user story does not require client approval by default. Client acceptance of an increment at Sprint Review is a separate recorded decision (BR-049). | Agreed direction; V2 |
| BR-018 | Each approval decision records request, target version, actor, decision, timestamp, and applicable policy. | Agreed direction |
| BR-019 | Review decisions require assigned authority for the exact revision under project policy. Visibility or a `Client` role alone does not make someone an approver. | User-confirmed: Q11/Q12/Q15 |
| BR-020 | Approved baselines must not be silently replaced by later versions. | Agreed direction |
| BR-021 | A material change to baselined scope or an agreed delivery commitment requires impact assessment and an authorized Change Request decision before applicable final acceptance. This includes agreed FR/NFR/Story criteria, even if UR text is unchanged. Routine edits within the agreement do not require a CR solely because a record changed. | User-confirmed: Q22/Q39/W1 |
| BR-022 | Rejected/withdrawn approvals must remain visible in audit history. | Proposed rule |
| BR-023 | In-app approval is not necessarily a legally binding electronic signature. | Agreed direction |

### Initial review policy selected in discovery

- Team internal review defaults to independent review; an authorized solo direct-acceptance exemption is recorded honestly and never waives required client approval (Q10/Q14).
- Every required reviewer approves the current revision; optional nonresponse does not block. Required reviewer removal/replacement is authorized and recorded (Q15).
- Client participation is explicitly Feedback Only or Approval Required under policy. Silence/expiry is never approval; feedback-only nonresponse creates no approval gate (Q11).
- Discussions are revision-bound, internal by default, and explicitly shared when client-visible. Mentions do not grant access (Q12/Q13).
- An ordinary comment differs from Changes Requested by a required reviewer. Author thread closure cannot clear that blocking decision; the reviewer clears/withdraws it (Q16).
- Authorized BA/PM final acceptance follows all applicable gates. Acceptance by an author cannot replace required independent review (Q14).

**Deferred or open:** Quorum voting, elaborate chains and advanced delegation are deferred. Withdrawal/rejection/expiry mechanics, comment/attachment policy, material-change thresholds and exact specification-only review authority remain feature refinement. Initial one-UR proposals do not settle later package/milestone approval. Detailed routing guidance A3/A4 remains proposed.

### Change impact must examine

Affected requirements and versions, Feature mappings, specifications/AC, tests/evidence, linked work, technical dependencies, cost (if tracked), effort, capacity, milestones and risks. CR approval permits a change path; it does not itself accept specifications, establish baselines, apply plans or prove feasibility. Rejection/deferment retains current baselines, with affected-work disposition made explicitly. See [workflow change control](deliverypilot-workflow-discovery.md#8-agreements-baselines-and-change-control).

## 6. Backlog, Agile delivery, and quality

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-024 | Product backlog can represent initiatives, epics, features, stories, defects, spikes, and technical work. | Agreed direction |
| BR-025 | Technical/security/DevOps/NFR work may exist independently from user stories. | Agreed direction |
| BR-026 | Priority, defect severity, MoSCoW scope class, and deadline must be modeled separately. | Agreed direction |
| BR-027 | MVP projects use Scrum: one team/calendar and at most one active Sprint; future Sprints may be planned. Kanban project mode (no Sprint required) is post-MVP; hybrid and several independently scheduled teams per project are deferred. | User-confirmed: Q8/Q25, narrowed by V1 |
| BR-028 | A Scrum project can configure Sprint duration within a maximum of one month. | Agreed direction |
| BR-029 | The Scrum Team retains ownership of Sprint Planning decisions; AI suggestions do not constitute commitments. | Agreed direction |
| BR-030 | Development complete (DoD), QA verified, client accepted (Sprint Review or UAT) and Released must remain distinguishable concepts. | Agreed direction |
| BR-031 | Work item links must prevent invalid dependency cycles when the relationship is defined as a blocking dependency. | Proposed rule |
| BR-032 | A team can refine/discover future scope while delivering already-ready work. | Agreed direction |

### Typical backlog relationships

- `Epic -> Feature -> User Story -> Task` is the suggested default decomposition, not a universal schema mandate.
- Work items may also be `Bug`, `Spike`, `Tech Debt`, `Security`, or `Infrastructure`.
- Separate `blocks`, `depends_on`, `related_to`, `duplicates`, `implements_requirement`, `verified_by`, and `included_in_release` semantics; not every relationship is parent-child.

### Quality and acceptance

Story-level acceptance criteria describe observable outcomes. Draft refinement and estimation are allowed before commitment. W1 places DoR for selected scope before planning and rechecks it before Sprint selection/Kanban pull; the detailed checkpoint interpretation is A2 review guidance. Standalone work has its own rationale and readiness; exceptions require visible authorization (Q23).

Dev/QA work and draft test scenarios cover relevant AC/FR/NFR/rules. Drafted scenarios are not executed tests. DoD, QA, applicable UAT and release retain separate evidence; failed required checks return to correction/reverification, while changed expectations follow change control (Q41/W1; detailed disposition A4). Full test-management and automated deployment suites are deferred, not the distinction between those facts.

Execution-board moves change permitted state or rank, not dates, acceptance or release. Material active-Sprint changes retain the original selection, delta, reason and explicit PM/team decision (Q28/Q30). Kanban WIP rules (Q40) apply when the post-MVP Kanban mode is delivered. Exact thresholds and states remain project/feature design.

### Scrum events and outsourcing engagement rules

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-046 | A client project belongs to at most one Client Engagement in the vendor's workspace; internal projects need none. The engagement's contract type is recorded and its changes are attributable. | User-confirmed: V3; field details proposed |
| BR-047 | Each Sprint records its Sprint Goal, original selection, start/end dates, declared capacity and outcome (completed, carried over, removed). Closed Sprint records are not silently rewritten. | Proposed rule |
| BR-048 | Velocity is computed per team from completed work in closed Sprints using the project's estimation method. It is a planning and signal input only, never a target or an individual measure. | Proposed rule; see BR-034/BR-044 |
| BR-049 | Sprint Review records per-item client feedback or acceptance by an assigned client decision-maker. Acceptance is bound to the reviewed increment item and does not imply QA, UAT or release. Silence is not acceptance. | User-confirmed: V2; Q11 principle |
| BR-050 | In MVP, client scope sign-off targets an explicit release or Sprint scope baseline (exact selected versions). Per-UR proposal review (Q4/Q9–Q16) remains valid behavior delivered later. | User-confirmed: V2 |
| BR-051 | Retrospective improvement actions have an owner and status, and may link to the risks or issues they address. | Proposed rule |

## 7. Estimation, planning, and resources

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-033 | Effort, duration, available capacity, deadline and forecast are distinct concepts. | Agreed direction |
| BR-034 | Story points must not be converted to hours using a universal ratio. | Agreed direction |
| BR-035 | Individual allocation to multiple projects must not double count availability. | Agreed direction |
| BR-036 | Capacity uses declared calendars, leave, allocations and obligations, including QA. Initial inputs may be maintained manually; private work in another workspace is not exposed or assumed known. | User-confirmed: Q27/W1 |
| BR-037 | Forecasts must show material assumptions, range/uncertainty where supported, and the reference baseline. | Agreed direction |
| BR-038 | Forecasts and draft scenarios do not change commitments. Applying a scenario requires current inputs, team feasibility review and authorized action; plan baselines are established separately. | User-confirmed: Q18/Q29/Q34/Q42 |

The planning experience supports explicit estimation methods/units and attributable changes. Story Points in W1 illustrate a method, not a universal mandate. Do not double count a parent and child estimates for the same work; range-estimate UI remains a proposal. Never treat hours as a guaranteed completion date (Q20/Q26; A2).

A usable schedule needs scope/priorities, milestone flexibility, calendars, estimates, declared capacity and dependencies/waits. Missing evidence produces a checklist or conditional scenario; point-based calendar forecasts require representative history. Infeasible targets produce explicit scope/date/sequence/available-capacity options, not invented staff, assumed overtime or reduced estimates. Recheck material inputs before scenario application (Q31–Q34). Roadmaps, milestones, Sprints and releases have distinct lifecycles rather than a mandatory hierarchy (Q24).

## 8. Risk, issue, blocker, and AI guardrails

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-039 | Risk, issue, blocker and dependency are distinct but linkable concepts. | Agreed direction |
| BR-040 | Deterministic checks should detect objective risks before invoking LLM interpretation where feasible. | Agreed direction |
| BR-041 | Candidate findings retain evidence and human review state; exact generation-metadata fields remain design work. A finding is not automatically a confirmed risk, issue or blocker. | User-confirmed behavior: Q35–Q38; metadata proposed |
| BR-042 | AI cannot autonomously approve requirements, reassign staff, change baselines, or communicate new client commitments. | Agreed direction |
| BR-043 | AI retrieval must enforce the same tenant and user authorization as interactive access. | Agreed direction |
| BR-044 | Workload data must not drive unreviewed employee performance scoring. | Agreed direction |
| BR-045 | Predictions should not claim calibrated confidence without adequate data and evaluation. | Agreed direction |

### Delivery risk management rules (V4 capability; broader positioning under D1)

| ID | Cross-feature requirement / rule | Status |
|---|---|---|
| BR-052 | The MVP risk-signal catalog includes at least: overdue client approval/decision, client-owned dependency waiting, scope added to an active Sprint without a recorded decision, work added outside the scope baseline without a CR, Sprint Goal at risk (remaining work vs. remaining capacity), velocity deviation from recent history, capacity overcommitment, blocked-work age, high carry-over, and effort/budget burn ahead of plan for the engagement's contract type. Each signal declares inputs, configurable criteria and the evidence it cites. | User-confirmed: V4 (families); thresholds proposed |
| BR-053 | Risk signals run without an AI provider. A provider outage must not stop detection, the risk register or manual reporting. | User-confirmed: V4; BR-040 |
| BR-054 | A confirmed risk records owner, affected scope (engagement, release, Sprint or work), probability, impact, response strategy (avoid, mitigate, transfer, accept), actions, due dates and history. | Proposed rule |
| BR-055 | Risk, finding and delivery-health information is internal by default. Sharing with the client is an explicit, attributable action on selected items; client-visible text never exposes internal estimates or notes not explicitly shared. | Proposed rule; BR-004 |
| BR-056 | AI risk insights (MVP+1) present observations, inferences, cited evidence and uncertainty separately. Suggested mitigations are drafts until a person acts on them. | User-confirmed: V4; BR-041/BR-042 |
| BR-057 | Delivery-health indicators must be decomposable into their contributing signals and reviewed risks; no opaque composite score. | Proposed rule |

Authorized reviewers confirm, link, dismiss or request information; material decisions and follow-up remain attributable. Reevaluate relevant changes, deduplicate findings, and notify meaningful new/worsening evidence instead of repeatedly resurfacing unchanged dismissed findings. Manual blocker reporting does not depend on AI; every explanation and notification obeys recipient access (Q35–Q38). AI may suggest eligible assignments or future Sprint placement but cannot apply them (Q42).

### Lifecycle, execution and engineering evidence

| ID | Rule | Authority |
|---|---|---|
| BR-058 | Initial product scope starts after contract signing. CRM, presales proposals, payroll, full accounting, source hosting and a proprietary load-testing engine are excluded. Referenced contract/payment/test evidence may be captured. | User-confirmed: D4–D6 |
| BR-059 | Support eight lifecycle/governance stages with continuous refinement, design, development and testing; UAT and releases may repeat for different scope. | User-confirmed: D3 |
| BR-060 | Preserve the original Sprint timebox. Visualizing actual execution does not extend that Sprint or overwrite its original plan. Preserve initial selection and record subsequent changes separately. | User-confirmed: D2 (timebox/separate view); selection history proposed |
| BR-061 | Distinguish original plan, current work, actual events, forecasts and scenarios. Show as-of time, source and unknown/stale data; an AI prediction is not an actual event. | Proposed rule |
| BR-062 | Closing a Sprint records unfinished work. Later selection is explicit; count a completed item once in velocity, retain original noncompletion and carry-over links. Reopened defects or split work must not duplicate delivered value. | Proposed rule |
| BR-063 | API specification revisions, deployed build/environment availability and consumer integration status are separate facts. A new revision notifies authorized affected consumers and requires compatibility re-evaluation; prior integration evidence remains historical. | Proposed rule derived from user API idea |
| BR-064 | Imported test evidence and exported reports identify API revision, build, environment, run time, scenario/configuration and provenance. External tools execute load tests. Never expose credentials from imported curl/specification text. | Proposed rule; D6 integration direction |
| BR-065 | Release, hypercare completion, warranty expiry, handover, final acceptance and commercial closure are separately evidenced. Closure may reference open obligations with an accountable owner. | Proposed rule |
| BR-066 | Search, reports, exports, notifications and AI retrieval enforce current tenant/project/resource permissions. Multi-organization identity does not allow employers to inspect one another's work or infer hidden assignments. | Proposed consolidation of existing access principles |
| BR-067 | AI may draft reports, risk explanations and planning/recovery scenarios. Humans review outputs; applying a plan or sending client communication is a separate authorized action. No autonomous baseline, timebox or approval changes. | Proposed consolidation; D4 AI assistance |

The [SDLC workflow](sdlc-workflow.md) provides examples and exceptions. These rules do not select module ownership, API endpoints or storage design.

## 9. Non-functional requirements to refine

These are **NFR categories**, not yet numeric SLAs:

| NFR ID | Category | Acceptance target to establish during discovery |
|---|---|---|
| NFR-001 | Tenant isolation | Unauthorized cross-tenant actions and reads fail across APIs, jobs, search, exports and AI retrieval |
| NFR-002 | Authorization | Resource-scoped RBAC/policies enforce least privilege, including external clients |
| NFR-003 | Auditability | Critical approval, version, scope, membership and release decisions have actor/time/version evidence |
| NFR-004 | Data integrity | Concurrency, revision immutability, referential consistency and transaction boundaries are testable |
| NFR-005 | Availability/recovery | Availability target, backup retention, RPO and RTO agreed with stakeholders |
| NFR-006 | Performance | Measurable p95 latency, report response time and background-job lag under defined workloads |
| NFR-007 | Scalability | Expected organizations, users, projects and work-item counts defined and load-tested |
| NFR-008 | Privacy | Retention/deletion/data residency and AI provider policies agreed before production use |
| NFR-009 | Observability | Correlation IDs, logs, metrics and traces support incident investigation |
| NFR-010 | Accessibility | Keyboard and assistive-technology behavior verified against selected standard |
| NFR-011 | Maintainability | Module contracts and tests support change without cross-module coupling |
| NFR-012 | Interoperability | Documented APIs, imports/exports and integration policy for selected providers |
| NFR-013 | AI evaluation and cost | Finding precision/false-positive measurement, evaluation datasets, per-analysis token/cost budgets, timeouts and provider fallback defined before AI insights are enabled for pilots |
| NFR-014 | Signal freshness | Maximum delay between a relevant delivery event and signal re-evaluation, and visible last-checked time, agreed per signal family |

**Do not invent targets** such as 99.9% availability or 300 ms p95 until workloads, hosting cost, support expectations and pilot acceptance needs are known.

## 10. Minimum user journeys requiring acceptance tests

1. **Organization isolation:** A member of Tenant A cannot enumerate, modify, export or retrieve Tenant B's artifacts, including via API guesses or queued work.
2. **Client access:** External approver sees only the intended shared requirement/package; unrelated artifacts remain inaccessible.
3. **Approval revision:** Approval of Requirement v1 does not automatically approve v2.
4. **Requirement traceability:** Requirement links lead to authorized features, stories and verification records without changing the requirement's ownership.
5. **Scrum delivery:** A Scrum team can plan a Sprint with a Sprint Goal and declared capacity, record material Sprint changes, and close the Sprint with completed/carried-over outcomes. (Kanban flow without Sprint enrollment is a post-MVP scenario.)
6. **Multi-project capacity:** A member's capacity is allocated across projects without double counting.
7. **AI guardrail:** A risk finding can be dismissed or accepted; its suggestion cannot directly rewrite an approved delivery baseline.
8. **Risk signal to decision:** Scope added to an active Sprint without a recorded decision raises a candidate finding with evidence; the PM confirms it as a risk with owner and response, and the record keeps the original evidence.
9. **Client-scoped health:** A client PO sees only the delivery-health items and risks explicitly shared for their engagement, never another engagement's data or internal notes.
10. **Sprint Review acceptance:** A client PO accepts one increment item and rejects another; acceptance does not mark the item QA-verified or released, and the rejected item returns to the backlog with feedback.

These are target-workflow acceptance themes, not assertions that every capability is allocated to MVP. Additional cases are in [workflow scenario seeds](deliverypilot-workflow-discovery.md#15-acceptance-scenario-seeds) and [Identity review scenarios](architecture/modules/identity/review-scenarios.md). Feature-level scenarios belong in `features/FEAT-DP-XXX-<slug>/us/US-DP-XXX-NNN.md`, linked to their FRs; `feature.md` remains a summary.

## 11. Open stakeholder questions

1. Which artifact types require client approval in fixed-price vs time-and-materials engagements?
2. What additional review policy is needed beyond the selected all-required-reviewers and client modes? Quorum/chains/advanced delegation remain deferred.
3. Which additional Client Portal resources should be explicitly shared? Unshared internal estimates, allocations, comments and unrelated projects remain private.
4. What exact DoR/DoD checks, exception permissions and QA/UAT/release evidence apply to each project policy?
5. Do pilot customers need full Product-to-many-Projects modeling, or is a Project-first model sufficient?
6. Which jurisdictions, data-retention periods, and AI-provider restrictions apply?
7. Which workload and success metrics are ethically and operationally appropriate for teams?
8. Which customer workflows justify integrations with GitHub, GitLab, Azure DevOps, Slack or Teams?
9. Which lightweight planning, baseline-change and delivery-evidence outcomes belong in the first pilot, given the current V1 feature assignments?
10. Which BR-052 signals and default thresholds do pilot vendors consider useful, and what false-positive rate is acceptable?
11. Which monetary and effort fields are needed for fixed-price pilots, and which are internal versus client-shared?
12. Which risk and health information will vendors share with clients by default?

The joint Organization review U9–U21 resolves its selected business scope; see [decision disposition](features/FEAT-DP-003-organization/feature.md#7-open-decisions-and-task-planning-blockers). Creation replay is protected for 24 hours with changed-content conflicts and explicit new intent after expiry. U19 selects post-commit/new-decision denial, admitted-read completion, Identity commit rechecks, job reauthorization and fail-closed unavailable facts. U20 selects minimal audit fields/reasons, required success-audit/write consistency and workspace-lifetime history. Membership restoration, workspace/account lifecycle deletion, automated purge/erasure and operator/platform recovery are explicitly outside this release, with denied-state/containment rules enforced. Organization backend contracts and persistence are documented in the [current Identity baseline](architecture/modules/identity/README.md#verified-implementation-baseline). Future Project/Portal integrations still need source-aware task design. Do not reopen selected multiple ownership, durable membership, role combination, invitation proof or pending-offer uniqueness as unanswered questions.

**Rule of use:** Do not convert questions or proposed rules into "approved" user requirements without stakeholder validation.

## 12. Additional acceptance scenarios to refine

- An unfinished item finishes in a later Sprint: original dates and noncompletion remain visible, and delivered points are counted once.
- A new release enters UAT while another is under warranty: decisions apply to the correct release scope.
- An API revision changes after one consumer integrated: historical integration stays attributable, with the new revision awaiting compatibility evidence.
- A user belongs to two vendors: searching their name in one tenant never reveals the other vendor’s work.
- A report generated from stale data identifies the gap and cannot silently change a client commitment.

These are scenario seeds, not executed tests. Monetary visibility, first connector selection, warranty terms and detailed review policies still need pilot validation.
