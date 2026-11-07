# Research plan: fixed-price outsourcing with Agile Scrum

**Status:** Research scope approved; execution authorized by the user.
**Prepared:** 2026-11-07.
**Audience:** Client and vendor project managers, Product Owners, delivery leads, and engagement managers.
**Perspective:** Practical guidance from a senior PM and PO responsible for commercial commitments and software delivery.

## 1. Objective and approval gate

Produce a practical reference for running an outsourced software project using Agile Scrum under a fixed-price contract, from pre-sales through maintenance. Explain the decisions, responsibilities, evidence, and controls needed to reconcile iterative delivery with commercial commitments.

This plan was the only deliverable created before approval. The user approved it with the message "approve" on 2026-11-07 in this conversation. That approval authorizes external research, source verification, the README index, and the nine topic documents. It does not approve the eventual findings as DeliveryPilot product requirements or project policy.

The remaining sections preserve the approved research scope and proposed method. The completed document set is indexed in [README.md](README.md).

The research will be general practitioner guidance. Its findings may inform later DeliveryPilot discussions; adopting them as product requirements or project policy would be a separate decision under the repository's [authoring policy](../authoring.md).

## 2. Proposed files and reading order

All files will live in `docs/research/`. Filenames below are planned outputs; links will be added to the README when the files exist.

| Order | File | Outline and practical outputs |
|---|---|---|
| Entry point | `README.md` | Purpose, audience, reading order, topic summaries, key terms, source conventions, and navigation to templates. |
| 00 | `00-research-plan.md` | This plan: scope, outline, research questions, source strategy, workflow, and completion criteria. |
| 01 | `01-overview.md` | Fixed scope/time/cost versus changing needs; commercial model comparison; fixed price per sprint or phase; capped time and materials (T&M); "money for nothing, change for free"; scope swapping; model selection examples. |
| 02 | `02-pre-sales-and-contracting.md` | Discovery and inception commercial arrangements; estimation; baseline backlog; assumptions and exclusions; risk pricing and contingency; payment milestones; acceptance and change clauses; worked pricing and payment example. |
| 03 | `03-sdlc-phases.md` | Discovery → Inception/Sprint 0 → Development sprints → Stabilization/UAT → Release → Warranty and handover → Maintenance; phase activities, deliverables, entry/exit criteria, and client/vendor ownership. |
| 04 | `04-scrum-operating-model.md` | Client PO and vendor proxy PO; Scrum Master, Developers, and PM responsibilities; Scrum events and refinement; Definition of Ready (DoR), Definition of Done (DoD), acceptance criteria, and sign-off. |
| 05 | `05-scope-and-change-management.md` | Baseline and backlog control; change requests; scope trade-offs; scope creep; approval authority; burn-up charts; budget forecasts; earned value; worked swap and paid-change examples. |
| 06 | `06-risk-quality-and-governance.md` | Risk register; QA strategy throughout delivery; reporting cadence; steering committee; decision rights; escalation path; sample risk entries and governance calendar. |
| 07 | `07-offshore-communication.md` | Time-zone overlap; asynchronous work; communication tools and records; status reports; demos and acceptance meetings; example weekly schedule and handoff format. |
| 08 | `08-templates-and-checklists.md` | Reusable SOW outline, change request form, sprint report, UAT sign-off, and project kickoff checklist, with completion guidance and example fields. |
| 09 | `09-lessons-learned-and-pitfalls.md` | Common failure modes, early warning signs, consequences, prevention, and recovery actions; practical PM/PO scenarios. |

The planned set contains eleven Markdown files: this plan, nine topic documents, and one README index. Each topic has one primary owner file; other chapters will cross-reference it to avoid conflicting guidance.

## 3. Key research questions by topic

### 01 — Overview and commercial models

- What is fixed in each model: scope, outcomes, capacity, deadline, price, or expenditure ceiling? What can change, and who carries the uncertainty?
- How can a contract support an evolving Product Backlog while preserving an agreed commercial baseline?
- How do fixed price per sprint, fixed price per phase, capped T&M, and scope swapping differ in payment basis, risk allocation, and suitability?
- What do "money for nothing" and "change for free" mean in their original context, and what conditions, limits, and incentives make them workable?
- When should a PM recommend further discovery, renegotiation, or a different commercial model?

### 02 — Pre-sales and contracting

- What should discovery validate before a binding delivery price is offered? How are paid discovery and subsequent delivery commitments separated?
- How do story points support team planning, while effort, staffing, rates, dependencies, and uncertainty support a commercial estimate? How should estimates be presented without assuming a universal points-to-hours conversion?
- What belongs in the baseline backlog, including nonfunctional requirements, integrations, migration, environments, and acceptance evidence? How much detail is needed before contracting?
- How should assumptions, exclusions, client obligations, third-party dependencies, and delayed decisions be recorded and priced?
- How should risk buffer, contingency, management reserve, and vendor margin be distinguished and governed?
- What payment milestones, acceptance windows, rejection/rework procedures, change request clauses, and termination arrangements make commitments measurable?

### 03 — SDLC from discovery to maintenance

- For each of the seven requested phases, what work happens, what evidence is produced, and what entry and exit criteria apply?
- Which client role and vendor role perform the work, own the decision, review the evidence, and authorize the transition?
- How do contractual phase gates coexist with iterative delivery and feedback? Which activities overlap or repeat?
- How are testing and integration performed during development, and what additional work remains for stabilization and user acceptance testing (UAT)?
- What distinguishes release readiness, business acceptance, operational handover, warranty obligations, and maintenance service commitments?
- How are defect versus enhancement decisions, warranty start/end conditions, knowledge transfer, access transfer, and outstanding work handled?

Every phase will use the same structure: purpose, activities, deliverables, entry criteria, exit criteria/evidence, client responsibilities, vendor responsibilities, and common blockers. A summary matrix will make ownership and handoffs easy to compare.

### 04 — Scrum in a fixed-price engagement

- What does the Scrum Guide prescribe, and which arrangements are optional organizational practices, including Sprint 0, DoR, proxy PO roles, and commercial sign-off?
- Who holds Product Owner accountability? What may a vendor proxy decide, and when must the client PO respond or escalate?
- How do Sprint Planning, Daily Scrum, Sprint Review, Sprint Retrospective, and ongoing refinement support the contract without obscuring team accountability?
- How should DoR, DoD, story acceptance criteria, and contractual acceptance criteria relate?
- How do a Done increment, Sprint Review feedback, client acceptance, UAT approval, release authorization, and invoice approval differ?
- What happens when a story misses acceptance criteria, the client requests something new during a sprint, or an authorized approver is unavailable?

### 05 — Scope and change management

- How are the contract baseline, current backlog, approved changes, and delivery forecast reconciled and versioned?
- How does the team classify a clarification, defect, enhancement, substitution, and paid change? Who resolves disputed classifications?
- What is the workflow from request through impact analysis, decision, authorization, implementation, and acceptance? What happens to unapproved work?
- What makes a scope swap commercially acceptable after accounting for effort, dependencies, regression testing, risk, and mandatory requirements?
- How do burn-up charts expose delivered work and changing scope? How are budget consumed, remaining cost, forecast at completion, and contingency use reported?
- When is earned value useful, how should planned value, earned value, and actual cost be defined, and how are approved baseline changes handled without masking overruns?
- How do reports distinguish client expenditure and contractual payments from vendor delivery costs and margin?

### 06 — Risk, quality, and governance

- Which outsourcing risks deserve early attention, including estimation uncertainty, unavailable client decisions, integration dependencies, turnover, and UAT delays?
- What fields, ownership, review cadence, triggers, mitigation actions, and contingency actions belong in the risk register? When does a risk become an issue?
- How should the QA strategy cover continuous testing, automation, security, performance, regression, defect triage, and release evidence?
- What should the team report each sprint and between sprints? Which decisions belong to delivery management, the client PO, commercial owners, or the steering committee?
- What severity thresholds, response expectations, and escalation path keep stalled decisions from silently consuming the schedule and budget?

### 07 — Offshore communication

- How much overlap is needed for timely decisions, and how should teams handle daylight-saving changes, local holidays, and out-of-hours fairness?
- Which decisions need synchronous discussion, and what information makes an asynchronous handoff actionable?
- What tool capabilities support the backlog, documents, decisions, defects, communications, and approval evidence? Where is the authoritative record for each?
- What should daily updates, weekly status reports, demos, and acceptance meetings contain, and who attends or signs?
- How are language differences, ambiguous feedback, missed meetings, and delayed client responses handled?

### 08 — Templates and checklists

- **Statement of Work (SOW):** What fields define outcomes, deliverables, baseline scope, exclusions, dependencies, responsibilities, price, milestones, acceptance, changes, warranty, and handover?
- **Change request:** What fields capture the reason, alternatives, affected baseline, impact on scope/cost/schedule/quality, decision authority, and approval evidence?
- **Sprint report:** How are the Sprint Goal, Done work, acceptance status, scope movement, financial forecast, risks, and decisions presented together?
- **UAT sign-off:** What identifies the tested version/environment, agreed criteria, execution evidence, open defects, acceptance decision, conditions, and authorized signatories?
- **Kickoff checklist:** What must be established across contract, roles, backlog, access, environments, quality, communication, governance, and escalation?

Each template will include instructions, blank fields that can be copied, and a short example or sample entries. The templates will use the same terms and authorities as the preceding chapters.

### 09 — Lessons learned and pitfalls

- What failure patterns recur when a team promises detailed scope, a date, and a price before resolving major uncertainty?
- How do weak client PO availability, hidden scope additions, proxy authority gaps, and ambiguous acceptance become commercial disputes?
- How do metric misuse, deferred testing, unrealistic velocity commitments, weak handover, and underpriced warranty obligations damage delivery?
- What can a senior PM/PO detect early, prevent, renegotiate, or recover? Which decisions should be escalated rather than absorbed by the team?

## 4. Research and evidence strategy

Research will start after approval. The following are intended source categories, not sources already verified or consulted for substantive findings.

| Source category | Intended use | Verification approach |
|---|---|---|
| Official Scrum Guide | Scrum accountabilities, events, artifacts, commitments, and the Definition of Done | Confirm the edition and publication details on the official Scrum Guides site; cite the relevant sections. |
| Project Management Institute (PMI) | Procurement, estimating, risk, governance, earned value, and hybrid delivery | Consult relevant official PMI guidance and articles, including the Agile Practice Guide and applicable standards where accessible; record editions and access limits. |
| Original contracting authors and recognized industry organizations | Fixed-price Agile models, scope trade-offs, early termination incentives, and practitioner experience | Seek original accounts, including the provenance of "money for nothing, change for free"; consider Scrum Inc., Scrum.org, Agile Alliance, and established industry practitioners where directly relevant. |
| Recognized delivery and quality guidance | Offshore collaboration, continuous quality, release, and handover | Prefer attributable practitioner guidance and original organizational publications; assess the context and limits of each recommendation. |

Evidence rules for the writing stage:

1. Verify relevant claims against accessible sources and retain direct links, author/organization, title, publication date or edition where available, and access date.
2. Distinguish framework requirements, commercial practices, practitioner recommendations, and illustrative examples. Explain conflicting guidance and the circumstances in which it applies.
3. Prefer primary sources. Do not use search snippets or an inaccessible publication as evidence for details that could not be checked.
4. End every completed topic file and the README with a **Sources** section containing relevant references. Use reference markers or nearby links for major claims so readers can identify supporting evidence.
5. Include the Scrum Guide, PMI, and recognized industry articles across the research set, selecting sources relevant to each chapter rather than adding unrelated citations.
6. Label sample clauses, approval thresholds, service expectations, and numeric examples as illustrative. Identify commercial assumptions and questions requiring project-specific agreement.

## 5. Practical examples and presentation

Use one fictional software outsourcing engagement throughout the guide. Establish its scope, currency, price, delivery schedule, sprint length, team capacity, milestone payments, contingency treatment, and acceptance authorities before writing numeric examples.

The shared example will support:

- A pricing breakdown and milestone payment schedule with reconciled totals.
- A baseline backlog, scope substitution, paid change request, and updated forecast.
- A burn-up example and a small earned-value calculation, with units and assumptions explicit.
- Sample risks, client decision delays, a sprint report, and an escalation scenario.
- A UAT acceptance decision, open-defect treatment, warranty boundary, and handover checklist.

Sample numbers will demonstrate mechanics and trade-offs rather than claim industry benchmarks. Use Markdown tables for comparisons, phase gates, responsibility matrices, and reports. Use short scenarios and copyable templates for decisions readers need to make in practice.

## 6. Execution after approval

1. Record the user's approval of the research scope and any requested adjustments without implying approval of future findings.
2. Verify sources and build a claim-to-source map for the nine topics.
3. Establish the shared terminology, fictional engagement assumptions, and client/vendor decision rights.
4. Draft `01` through `09` in reading order, one topic per Markdown file, with sources at the end of each.
5. Create `README.md` with working relative links, reading guidance, and navigation to the templates.
6. Review cross-file consistency, calculations, scope coverage, source support, responsibility assignments, and distinctions between Scrum and contractual practices.
7. Run the repository documentation validator when available, check Markdown links and formatting, and report actual results and any remaining evidence gaps.

## 7. Completion criteria

- Every requested topic is covered in its designated file, and the README links to all eleven files.
- All seven SDLC phases include activities, deliverables, entry/exit criteria, and explicit client/vendor responsibilities.
- All requested contract models are compared by what is fixed, what may change, payment basis, risk allocation, and suitable conditions.
- The five requested templates are reusable and consistent with the guide's processes and authority model.
- Estimation, pricing, payments, contingency, burn-up, and earned-value examples use clear assumptions and consistent arithmetic.
- Scrum completion, client acceptance, release decisions, warranty obligations, and payment approval are explained with distinct evidence and decision owners.
- Each completed topic file and the README ends with verified, relevant sources; examples and recommendations are labeled appropriately.
- The research remains identifiable as research, and validation results and unresolved source limitations are reported accurately.

## Sources and inputs for this plan

- User's research brief in this conversation: requested scope, topic list, output format, and explicit approval gate.
- [Repository instructions](../../AGENTS.md): documentation workflow, validation, and evidence boundaries.
- [Documentation authoring policy](../authoring.md): authority, evidence labeling, and approval provenance.
- [Repository README](../../README.md) and [context map](../context-map.md): repository orientation and context routing.

Section 4 records the source strategy approved before research began. Verified external sources and substantive findings are recorded in the individual topic files; this plan preserves the research questions and agreed outputs.
