# DeliveryPilot - Product Blueprint

> **Version:** 2.1 (lifecycle and execution visibility reconciled 2026-11-07)
>
> **Status:** Current product direction under D1–D6, reviewed 2026-11-08. Compatible earlier Q/W/V and selected Identity choices retain their recorded scope. Detailed approval and customer validation remain separate.
>
> **Product type:** Multi-tenant B2B SaaS  
> **Primary market hypothesis:** Software outsourcing vendors running Scrum delivery for external clients (V5, not yet validated)  
> **Document owner:** Product Owner  
> **Related documents:** [Requirements](requirements.md) | [Workflow discovery](deliverypilot-workflow-discovery.md) | [Architecture workspace](architecture/README.md) | [Roadmap](roadmap.md)

[Decisions D1–D6](deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions) establish the current eight-stage, post-contract product direction. They supersede the risk-first positioning and seven-stage summary where those conflict; earlier Q/W/V decisions otherwise retain their scope. Detailed artifact approval, customer validation and implementation remain separate. The [SDLC workflow](sdlc-workflow.md) owns stage gates and execution visibility; [Identity decisions](architecture/modules/identity/design-decisions.md) retain their access/design authority.

## 1. Product definition

**DeliveryPilot is a multi-tenant, AI-assisted software delivery management platform designed for outsourcing organizations executing fixed-price software projects using Agile Scrum.**

It provides end-to-end visibility from project initiation to warranty and closure, connecting contractual scope, requirements, delivery planning, engineering execution, client approvals, and project health.

DeliveryPilot helps teams understand what was agreed, what is being delivered, what has changed, what is at risk, and what action should be taken next.

**North star:** Every delivery commitment is traceable from agreement through execution, acceptance and closure, with evidence for the next decision.

DeliveryPilot should answer:

- What did we agree to deliver to this client, under which contract type, and what has changed since?
- Is the current Sprint Goal on track? Is the release or milestone on track?
- Which risks threaten delivery right now, what evidence supports them, and who owns the response?
- Is scope creeping, is velocity drifting, is budget or effort burning faster than planned?
- Which client decisions, approvals or deliverables are we waiting on?
- What did the client accept at Sprint Review, and what is ready to release?
- What action is recommended next, and what evidence supports it?

## 2. Problem and opportunity

The initial product addresses fixed-price outsourcing after contract signing. Research suggests recurring coordination problems; their prevalence and importance for our target customers still need validation. Candidate problems include:

- **Scope creep:** work added mid-Sprint or outside the agreed baseline without a recorded change decision.
- **Client-side latency:** pending approvals, clarifications, environments or deliverables owned by the client.
- **Velocity and capacity drift:** team rotation, leave, multi-project allocation and key-person dependency.
- **Budget/effort burn:** spend or effort consumed faster than delivered value, especially on fixed-price work.
- **Opaque status:** status reports are assembled by hand from tools that do not connect commitments, work and risks.

Teams can configure existing trackers and service tools to cover parts of this workflow. Our hypothesis is that connecting agreements, execution and client decisions with less reconciliation work provides value. Differentiation to validate with pilots:

1. **Explainable delivery risk management**: deterministic risk signals, a human-reviewed risk register, and AI explanation and mitigation suggestions.
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
| Product Owner (vendor proxy) | Vendor | Refine and order work within explicit delegation; does not automatically inherit client acceptance or commercial authority |
| Scrum Master | Vendor | Facilitate Scrum events, remove impediments, keep the risk register current |
| Business Analyst | Vendor | Refine backlog items, acceptance criteria and requirements |
| Tech Lead / Architect | Vendor | Assess feasibility, technical dependencies and technical risk |
| Developer / QA / DevOps | Vendor | Deliver, verify and release increments; report blockers |
| Client Product Owner | Client | Clarify priorities, sign off scope baselines, accept increments at Sprint Review |
| Client Sponsor / Stakeholder | Client | Own business outcomes, funding and escalated trade-offs; follow shared health |
| Client QA / UAT | Client | Validate agreed scenarios and record evidence; sign off only if designated |
| Client Finance | Client | Check milestone evidence against payment terms; distinct from product acceptance authority |

A job title is **not** an authorization role. Organization membership, project participation, explicit permissions, resource visibility and decision assignments determine access. A System Administrator has separately controlled platform authority; it grants neither customer-content access nor business approval authority.

## 4. Organization, engagement, and project model

- **Workspace** is the user-facing term for **Organization**; **tenant** is its ownership and isolation boundary. The vendor owns the workspace.
- Registration establishes a global User identity. Creating a workspace is a separate action; the creator becomes its initial Owner. Multiple Owners are allowed, with last-owner protection.
- A **Client Organization** is an external business relationship recorded by the vendor. It is not a co-owner of vendor data and gains no access implicitly.
- A **Client Engagement** (V3) records the commercial delivery context for one client: signed agreement reference, fixed price, agreed scope, assumptions, budget/effort, milestones, acceptance/payment conditions and the client-side Product Owner. Time & Materials and Dedicated Team are retained future concepts. Native invoicing, ledger and payment processing are outside the initial implementation; external financial evidence can be referenced.
- One engagement can contain one or more **projects**. Each MVP project has exactly one owning tenant and belongs to at most one engagement. Internal (non-client) projects are allowed without an engagement.
- A global User can hold memberships in multiple organizations and participate in multiple projects. Permissions, availability and assignments remain scoped: one employer cannot inspect another employer’s projects or workload. Cross-organization availability sharing needs explicit consent and policy.
- Workspace membership does not grant project access. Client stakeholders participate as restricted guests with resource-scoped access through the Client Portal. Visibility, required-reviewer assignment and approval permission are separate facts.
- A **product** is the evolving software outcome; a **project** is a bounded delivery initiative. The MVP may keep the Product Goal within the project.
- MVP projects use **Scrum** (V1): one delivery team and calendar, at most one active Sprint, and future Sprints that may be planned. Kanban project mode, hybrid delivery and multiple independently scheduled teams per project are deferred.

**Example:** Vendor A runs a fixed-price ERP engagement for Client B. Client B's Product Owner accepts increments at Sprint Review and sees the shared release health. They cannot see Vendor A's internal estimates, the risk notes marked internal, or other clients' engagements.

## 5. Eight lifecycle and governance stages

The [SDLC workflow](sdlc-workflow.md) defines activities, artifacts, client/vendor authority, entry/exit gates and exception routes for:

1. Initialization.
2. Requirements analysis and validation.
3. Solution design and delivery planning.
4. Iterative Scrum delivery.
5. System testing and UAT.
6. Release and deployment.
7. Hypercare and warranty.
8. Project closure and handover.

These are lifecycle/governance stages, not a strict waterfall workflow. Refinement, design, development and testing continue during Sprints. UAT and deployment may repeat per release; development and warranty can coexist for different delivered scope.

Preserve the original Sprint start/end dates and selected-work snapshot. A separate execution timeline shows actual work, blocked intervals, carry-over and later completion; forecasts and recovery scenarios remain distinct from facts. A late item does not extend the Sprint.

Done (DoD), QA verified, client accepted, released, warranty complete and commercially closed remain separate facts. Daily Scrum serves Developers; PM status/reporting views can reuse its evidence without turning that event into an approval meeting.

## 6. Critical product concepts

| Concept | Meaning / boundary |
|---|---|
| Client Engagement | Commercial delivery context with a client: contract type, planned budget/effort, milestones, client PO |
| Contract type | Fixed-price in the initial product; T&M and Dedicated Team are future concepts, not initial supported workflows |
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

### Journey C - Delivery risk intervention

1. Delivery events (Sprint progress, blockers, approvals, capacity, scope changes, effort/budget burn) feed deterministic risk signals.
2. A signal produces a candidate finding with evidence and affected Sprint, release, engagement or work.
3. Optional AI explains the finding, links related evidence and suggests mitigation options, with uncertainty stated (MVP+1).
4. The PM or Scrum Master confirms, links, dismisses or requests information. Confirmed risks get an owner, exposure rating and response.
5. Responses that change a commitment go through a Change Request. Nothing changes commitments automatically.
6. Selected risk and health information is explicitly shared with the client; the outcome is recorded for later learning.

### Journey D - Sprint Review and client acceptance

1. The team presents the increment; the client PO accepts, rejects or gives feedback per item.
2. Rejected items return to the backlog with feedback; new needs become backlog items or Change Requests.
3. Accepted, released and Done remain distinct evidence. Velocity uses completed work under the team’s DoD, not client sign-off; review feedback informs scope and risk decisions.

## 8. Product scope and proposed delivery sequence

**Initial product scope:** Post-contract project delivery across all eight stages, requirements collaboration, Scrum planning/execution, client acceptance, risks, traceability, reporting and AI assistance. Contract inputs are recorded or referenced; presales is research context, not an in-product stage.

| Capability area | Coverage |
|---|---|
| Organization and project access | Multi-organization identity, separate memberships/project grants and scoped client collaboration |
| Agreement and requirements | Epics/features, versioned requirements/stories, acceptance criteria, optional vendor/client review policy, scope baselines and controlled change |
| Delivery and execution visibility | Tasks/technical work, Sprint timeboxes, capacity, workload, burndown, blockers, actual execution and separate forecasts |
| Quality and engineering evidence | API specification revisions, build/environment availability, consumer integration status, external test results and export |
| Lifecycle decisions | UAT, release, warranty obligations, handover and closure evidence |
| Shared understanding | Permission-aware search, weekly/Sprint reports, project health and client decision queues |
| AI assistance | Evidence-backed risk explanations, draft reports, Sprint planning and recovery scenarios under human control |

**Proposed first usable increment (MVP):** Secure engagement-to-Sprint-Review journey with baselines, lightweight change control, client decisions, original-versus-actual execution visibility, manual evidence links and health reporting. It is a first delivery slice, not complete implementation of the eight-stage product.

**Proposed subsequent increments:** AI risk/report assistance; detailed requirement review; QA/UAT/release; forecast scenarios; API collaboration; warranty and closure; deeper integrations and planning assistance. The [roadmap](roadmap.md) records feature allocation and gaps. Automated connector coverage is selected with pilot teams; the product accepts referenced evidence before every connector exists.

**Outside the initial implementation:** CRM, presales proposals, payroll, full accounting, source-code hosting and a proprietary load-testing engine. Native invoices/payments remain external. Kanban mode, multiple independent teams per project, cross-tenant co-ownership, custom workflow builders and portfolio planning remain deferred under earlier decisions. No autonomous AI commitments or employee ranking.

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

1. **Delivery continuity.** Connect agreements, execution, acceptance and closure; surface threats early with evidence.
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
- Which monetary and effort fields pilots need, which are vendor-private, and which may be shared with clients.
- Whether Sprint-level or release-level scope sign-off fits pilot contracts.
- Whether Kanban demand (maintenance and support engagements) justifies earlier delivery than V2.
- Data residency, regulatory constraints, AI-provider restrictions and first engineering/test integrations. Native billing is excluded initially; finance evidence depth remains to validate.
- MVP team, infrastructure budget and target dates.

**Change discipline:** Update this file when the *product direction or business rationale* changes. Put cross-feature behavior in [requirements.md](requirements.md), evidence in [workflow discovery](deliverypilot-workflow-discovery.md), module design in the [architecture workspace](architecture/README.md), topology alternatives in [architecture.md](architecture.md), and release allocation in [roadmap.md](roadmap.md). Preserve each source's authority label.
