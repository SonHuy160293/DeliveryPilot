# DeliveryPilot - Product Blueprint

> **Version:** 2.0 (vision repositioned 2026-11-06; previous baseline 1.1 reconciled 2026-11-03)
>
> **Status:** Product direction incorporates user-confirmed workflow (Q1–Q44, W1), selected Identity design, and the vision repositioning decisions V1–V5. Detailed artifact approval and customer validation remain separate.
>
> **Product type:** Multi-tenant B2B SaaS  
> **Primary market hypothesis:** Software outsourcing vendors running Scrum delivery for external clients (V5, not yet validated)  
> **Document owner:** Product Owner  
> **Related documents:** [Requirements](requirements.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md) | [Roadmap](roadmap.md)

Workflow Q1–Q44 and W1 supply recorded product-owner direction. [Vision decisions V1–V5](deliverypilot-workflow-discovery.md#196-vision-repositioning-decisions) narrow their release allocation: Scrum-only MVP, lightweight client sign-off, a minimal Client Engagement, and delivery risk management as the primary differentiator. The [Identity decision log](architecture/modules/identity/design-decisions.md) records module/access choices. Neither source establishes that a feature is implemented. See the [alignment audit](architecture/documentation-alignment-audit.md) for remaining handoffs.

## 1. Product definition

**DeliveryPilot is an SDLC Agile Scrum project management platform for software outsourcing vendors, with AI-powered delivery risk management.** It lets a vendor run Scrum delivery for each client engagement, from agreed scope through Sprints, Sprint Reviews and releases, while continuously detecting, explaining and tracking the risks that threaten what was promised to the client.

**North star:** *Every outsourced Sprint commitment is visible, measurable, and its delivery risk is detected early enough to act.*

**Value proposition:** Run client engagements on Scrum, know which commitments are at risk and why, and decide what to do before the client finds out.

DeliveryPilot should answer:

- What did we agree to deliver to this client, under which contract type, and what has changed since?
- Is the current Sprint Goal on track? Is the release or milestone on track?
- Which risks threaten delivery right now, what evidence supports them, and who owns the response?
- Is scope creeping, is velocity drifting, is budget or effort burning faster than planned?
- Which client decisions, approvals or deliverables are we waiting on?
- What did the client accept at Sprint Review, and what is ready to release?
- What action is recommended next, and what evidence supports it?

## 2. Problem and opportunity

Software outsourcing vendors deliver for external clients under explicit commitments: fixed scope and price, time-and-materials budgets, or dedicated-team capacity. Delivery problems usually surface late, after a Sprint has already slipped or a client escalates. Typical causes:

- **Scope creep:** work added mid-Sprint or outside the agreed baseline without a recorded change decision.
- **Client-side latency:** pending approvals, clarifications, environments or deliverables owned by the client.
- **Velocity and capacity drift:** team rotation, leave, multi-project allocation and key-person dependency.
- **Budget/effort burn:** spend or effort consumed faster than delivered value, especially on fixed-price work.
- **Opaque status:** status reports are assembled by hand from tools that do not connect commitments, work and risks.

General-purpose trackers manage tasks but do not tie Scrum execution to client commitments, nor do they explain delivery risk with evidence. DeliveryPilot's differentiation to validate with pilots is:

1. **Explainable delivery risk management** (primary): deterministic risk signals, a human-reviewed risk register, and AI explanation and mitigation suggestions.
2. **Outsourcing-native Scrum:** engagements, contract type, client Product Owner and Sprint Review acceptance are first-class.
3. **Commitment integrity:** version-bound scope baselines and Change Requests prevent silent scope changes.
4. **Scoped client transparency:** clients see the shared delivery health, scope and decisions intended for them, and nothing else.

A Scrum board alone is not the differentiator.

## 3. Customers, buyers, and users

**Initial ICP hypothesis (V5, not validated):** Software outsourcing vendors with roughly 10–200 staff, several concurrent client engagements, Scrum delivery, and recurring client sign-off.

The same workspace model supports a solo freelancer and a vendor team (Q1/Q10); this does not change the ICP hypothesis.

| Persona | Side | Main jobs to be done |
|---|---|---|
| Executive / CTO | Vendor | See engagement portfolio health and the delivery risks needing attention |
| Account / Engagement Manager | Vendor | Own the client relationship, contract type, budget and escalations |
| Delivery Manager | Vendor | Coordinate commitments, shared people and cross-engagement risk |
| Project Manager | Vendor | Maintain releases, milestones, dependencies, risks and client communication |
| Product Owner (vendor proxy) | Vendor | Order the Product Backlog and represent client value when the client PO is unavailable |
| Scrum Master | Vendor | Facilitate Scrum events, remove impediments, keep the risk register current |
| Business Analyst | Vendor | Refine backlog items, acceptance criteria and requirements |
| Tech Lead / Architect | Vendor | Assess feasibility, technical dependencies and technical risk |
| Developer / QA / DevOps | Vendor | Deliver, verify and release increments; report blockers |
| Client Product Owner | Client | Clarify priorities, sign off scope baselines, accept increments at Sprint Review |
| Client Sponsor / Stakeholder | Client | Follow shared delivery health, decisions and change requests |

A job title is **not** an authorization role. Organization membership, project participation, explicit permissions, resource visibility and decision assignments determine access. A System Administrator has separately controlled platform authority; it grants neither customer-content access nor business approval authority.

## 4. Organization, engagement, and project model

- **Workspace** is the user-facing term for **Organization**; **tenant** is its ownership and isolation boundary. The vendor owns the workspace.
- Registration establishes a global User identity. Creating a workspace is a separate action; the creator becomes its initial Owner. Multiple Owners are allowed, with last-owner protection.
- A **Client Organization** is an external business relationship recorded by the vendor. It is not a co-owner of vendor data and gains no access implicitly.
- A **Client Engagement** (V3) records the commercial delivery context for one client: contract type (Fixed-price, Time & Materials, Dedicated Team), planned budget or effort, milestones and the client-side Product Owner. Billing and invoicing are out of scope.
- One engagement can contain one or more **projects**. Each MVP project has exactly one owning tenant and belongs to at most one engagement. Internal (non-client) projects are allowed without an engagement.
- Workspace membership does not grant project access. Client stakeholders participate as restricted guests with resource-scoped access through the Client Portal. Visibility, required-reviewer assignment and approval permission are separate facts.
- A **product** is the evolving software outcome; a **project** is a bounded delivery initiative. The MVP may keep the Product Goal within the project.
- MVP projects use **Scrum** (V1): one delivery team and calendar, at most one active Sprint, and future Sprints that may be planned. Kanban project mode, hybrid delivery and multiple independently scheduled teams per project are deferred.

**Example:** Vendor A runs a fixed-price ERP engagement for Client B. Client B's Product Owner accepts increments at Sprint Review and sees the shared release health. They cannot see Vendor A's internal estimates, the risk notes marked internal, or other clients' engagements.

## 5. Scrum across the software delivery lifecycle

Scrum is the delivery framework; SDLC activities happen inside and around it rather than as waterfall gates.

| Scrum element | DeliveryPilot capability | Representative outputs |
|---|---|---|
| Engagement and Product Goal | Client engagement, project setup, Product Goal, stakeholders | Engagement record, project brief |
| Product Backlog | Epics/Features/Stories/Bugs/Spikes/technical work, ordering, requirements links, DoR | Ordered, traceable backlog |
| Scope baseline and change | Client sign-off of release/Sprint scope; Change Requests | Signed-off baseline, CR decisions |
| Sprint Planning | Sprint Goal, capacity, velocity history, selection, readiness check | Sprint Backlog and Goal |
| Sprint execution / Daily Scrum | Board, assignments, blockers, Sprint changes with delta | Progress and impediment evidence |
| Sprint Review | Increment demo, client acceptance or feedback, backlog adaptation | Accepted/rejected increment items |
| Sprint Retrospective | Improvement actions and their follow-up | Recorded actions |
| Verification and release | DoD, QA, applicable UAT, release record | Quality and release evidence |
| Delivery risk management (continuous) | Signals, findings, risk register, mitigations, delivery health | Evidence-backed risks and decisions |

**Key distinction:** Done (DoD), QA verified, client accepted at Sprint Review or UAT, and Released remain separate facts.

## 6. Critical product concepts

| Concept | Meaning / boundary |
|---|---|
| Client Engagement | Commercial delivery context with a client: contract type, planned budget/effort, milestones, client PO |
| Contract type | Fixed-price, Time & Materials or Dedicated Team; changes which risk signals and thresholds apply |
| Product Goal / Sprint Goal | Long-term objective / commitment for one Sprint |
| Product Backlog / Sprint Backlog / Increment | Scrum artifacts; requirements remain separate linked artifacts |
| Requirement (UR/FR/NFR) and business rule | Need, behavior, quality constraint, policy; version-bound when agreed |
| Scope baseline | Explicit selection of agreed artifact versions signed off by the client; acceptance alone does not establish it |
| Change request | Controlled proposal to change a baseline or commitment, with impact assessment |
| Velocity | Historical completed estimate per Sprint for one team; a planning input, never a target or an individual metric |
| Capacity | Declared available time for a Sprint, after leave and allocations |
| Risk signal | Deterministic, explainable detection of an objective condition (for example, an approval overdue by N days) |
| Candidate finding | Evidence-backed concern awaiting human review; not automatically a risk |
| Risk / issue / blocker / dependency | Potential event / occurred problem / active obstruction / work relationship |
| Risk exposure | Reviewed probability × impact rating with an owner and response |
| Delivery health | Explainable summary of a Sprint, release or engagement, derived from signals and reviewed risks; never a score of individuals |
| Release | Recorded delivery of software, distinct from Sprint completion |

The canonical cross-feature constraints are in [requirements.md](requirements.md).

## 7. Core user journeys

### Journey A - Engagement to Sprint

1. A vendor Owner creates the workspace; an authorized user records the Client Engagement (contract type, budget/effort, milestones, client PO) and creates the Scrum project.
2. The team builds the Product Backlog: Epics/Features/Stories with acceptance criteria and linked requirements where useful.
3. The client PO signs off the release or Sprint scope baseline for the exact versions selected (V2). Silence is not approval.
4. At Sprint Planning the team sets a Sprint Goal, checks declared capacity against velocity history and readiness, and selects the Sprint Backlog.
5. The team executes, reports blockers and records material Sprint changes with their reason and delta.

### Journey B - Client scope change

1. The client or the team proposes a change to baselined scope or a commitment.
2. The proposed content stays separate from the current agreement.
3. The team assesses the impact on backlog, effort, Sprint/release dates, budget burn (by contract type) and risk.
4. Authorized decision-makers approve, reject or defer. Approval does not itself apply a new plan or baseline.
5. Explicit reconciliation updates the backlog and baseline. Routine edits within the agreement stay lightweight.

### Journey C - Proactive delivery risk intervention (primary)

1. Delivery events (Sprint progress, blockers, approvals, capacity, scope changes, effort/budget burn) feed deterministic risk signals.
2. A signal produces a candidate finding with evidence and affected Sprint, release, engagement or work.
3. Optional AI explains the finding, links related evidence and suggests mitigation options, with uncertainty stated (MVP+1).
4. The PM or Scrum Master confirms, links, dismisses or requests information. Confirmed risks get an owner, exposure rating and response.
5. Responses that change a commitment go through a Change Request. Nothing changes commitments automatically.
6. Selected risk and health information is explicitly shared with the client; the outcome is recorded for later learning.

### Journey D - Sprint Review and client acceptance

1. The team presents the increment; the client PO accepts, rejects or gives feedback per item.
2. Rejected items return to the backlog with feedback; new needs become backlog items or Change Requests.
3. Accepted, released and Done remain distinct evidence; the review feeds velocity and risk signals.

## 8. MVP and release strategy

**MVP goal:** A vendor can run one client engagement in Scrum, from engagement setup through signed-off scope, Sprint Planning with capacity, execution, Sprint Review acceptance and controlled change, while rules-based delivery risk signals and a reviewed risk register expose threats to the Sprint Goal and release in a secure, auditable, client-scoped workspace. See the [roadmap](roadmap.md).

**MVP:** User/organization/membership; Client Engagement and project setup; Product Backlog with requirements links; lightweight client scope sign-off and baselines; Sprint capacity and velocity; Scrum delivery (planning, board, Sprint changes); Sprint Review/Retrospective records; lightweight Change Requests; deterministic risk signals and risk register; delivery health view; scoped Client Portal; traceability.

**MVP+1:** AI risk insights (explanations, related evidence, mitigation suggestions) under the AI guardrails.

**Next release (V1):** Per-UR proposal review workflow, QA/UAT and release management, Sprint/release forecasting, AI planning assistant and evaluation.

**Later (V2):** Kanban project mode, cross-engagement resource planning, portfolio analytics, integrations, probabilistic predictions, commercial/billing extensions and enterprise governance, when validated.

**MVP non-goals:** Billing and invoicing, Kanban project mode, hybrid delivery, multiple teams per project, cross-organization co-ownership, custom workflow builders, autonomous AI actions, and automated employee scoring.

## 9. AI and risk-management principles

- **Rules first.** Objective conditions (overdue client approval, capacity exceedance, scope added mid-Sprint, blocked age, burn ahead of plan) are detected by deterministic, configurable rules that work without an LLM.
- **AI interprets; it does not decide.** AI explains findings, connects evidence, drafts mitigations and status narratives. Humans confirm risks and own responses.
- Every finding links to authorized evidence, states what was observed versus inferred, and shows uncertainty.
- Human authority covers baselines, assignments, client communications, and delivery commitments. AI cannot change them or promise dates to clients.
- Apply tenant and user authorization before retrieving content for AI, and treat client-supplied text as untrusted input.
- Do not claim calibrated predictions or confidence without representative history and evaluation.
- Never use velocity, workload or risk data to score or rank individuals.
- Measure precision, false positives, time-to-detection and cost before expanding AI scope.

See the [target architecture](architecture.md) for proposed technical guardrails and the [architecture workspace](architecture/README.md) for selected module design.

## 10. Success criteria and measures

**Customer outcome hypotheses:** Earlier detection of material delivery risk; fewer unrecorded scope changes; higher Sprint Goal and milestone predictability; faster client sign-off; less manual status reporting.

Candidate product measures:

- Risk: time-to-detection before impact, finding precision and false-positive rate, accepted mitigation rate, share of risks with owner and response.
- Delivery: Sprint Goal hit rate, committed-vs-completed variance, carry-over rate, milestone predictability, blocked-work age.
- Scope: scope-creep ratio (work added without a CR), CR turnaround, baseline change count.
- Client: sign-off lead time, Sprint Review acceptance rate, client portal engagement.
- Commercial (by contract type): effort/budget burn versus plan.
- Adoption: activated workspaces, active engagements, pilot-to-paid conversion; AI cost per analysis.

Define precise measurement methods before treating these as targets. Metrics must not rank individuals by story points, hours or velocity.

## 11. Product principles

1. **Risk visibility first.** Surface threats to commitments early, with evidence.
2. **Scrum-native, outsourcing-aware.** Model Scrum events and artifacts alongside engagements, contract types and client roles.
3. **Protect the agreement.** Version-bound baselines and Change Requests; no silent scope changes.
4. **Human decision, AI assistance.** No autonomous change to commitments, assignments or client communication.
5. **Security by design.** Tenant isolation and scoped client access are foundational.
6. **Evidence before certainty.** Expose sources, assumptions and uncertainty.
7. **Ship vertical slices.** Deliver complete engagement journeys over isolated technical modules.
8. **Design for evolution.** Clear boundaries without premature distributed complexity.

## 12. Open product hypotheses

These need customer evidence and/or formal acceptance:

- Actual ICP, buyer (CTO, delivery head or account manager), purchasing process and willingness to pay.
- Which risk signals pilot vendors value most, and acceptable false-positive levels.
- Minimum client-facing visibility: which health, risk and scope information vendors are willing to share.
- Engagement fields needed per contract type, and whether budget is tracked as money, effort or both.
- Whether Sprint-level or release-level scope sign-off fits pilot contracts.
- Whether Kanban demand (maintenance and support engagements) justifies earlier delivery than V2.
- Billing, data residency, regulatory constraints, AI-provider restrictions and integrations (Jira/Azure DevOps/GitHub import).
- MVP team, infrastructure budget and target dates.

**Change discipline:** Update this file when the *product direction or business rationale* changes. Put cross-feature behavior in [requirements.md](requirements.md), evidence in [workflow discovery](deliverypilot-workflow-discovery.md), module design in the [architecture workspace](architecture/README.md), topology alternatives in [architecture.md](architecture.md), and release allocation in [roadmap.md](roadmap.md). Preserve each source's authority label.
