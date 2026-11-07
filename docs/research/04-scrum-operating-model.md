# 04 — Scrum operating model for a fixed-price engagement

[Research index](README.md) · [Previous: SDLC phases](03-sdlc-phases.md) · [Next: Scope and change management](05-scope-and-change-management.md)

**Status:** Research-supported guidance. Atlas role assignments, meeting durations, decision deadlines, checklists, and acceptance records below are illustrative working arrangements.

## Establish the framework and the contract interface

Scrum defines a Product Owner, Scrum Master, and Developers. The PO remains accountable when backlog work is delegated. Developers organize delivery; the Scrum Master supports Scrum and team effectiveness. The Product Goal, Sprint Goal, and Definition of Done are commitments associated with the Product Backlog, Sprint Backlog, and Increment respectively. A Sprint Review must not become a release gate. These framework boundaries come from the November 2020 Scrum Guide. [Source: Scrum Guide](https://scrumguides.org/scrum-guide.html).

For an outsourcing engagement, add a clear commercial interface: who can accept deliverables, amend the SOW, approve extra expenditure, and release payment. Job titles alone do not convey those powers. Record named people, deputies, limits, and evidence requirements at kickoff.

## Client and vendor responsibilities

The following assignment is proposed for Atlas. A different project can appoint a vendor employee as PO, but it still needs an explicit arrangement for client authority and business access.

| Role | Atlas responsibility | Authority boundary and practical safeguard |
|---|---|---|
| Client Product Owner | Own product ordering and product decisions; reconcile stakeholder needs; clarify examples; arrange users for feedback. | Named as the single PO. May clarify behavior within the agreed baseline; escalates contractual scope, money, and date changes. |
| Vendor BA acting as proxy | Prepare stories, map questions, facilitate refinement, maintain decision records, and relay client decisions. | Delegation lists permitted clarifications. Cannot silently invent acceptance rules or approve extra commercial scope. Unanswered decisions stay visible. |
| Scrum Master | Coach the team and stakeholders; improve collaboration; surface impediments; facilitate effective events when useful. | An impediment requiring client procurement or sponsor intervention goes to that authority; the Scrum Master cannot promise its resolution date. |
| Developers, including engineering/QA/DevOps skills | Select and organize implementation work; provide estimates and technical evidence; integrate and verify each slice. | Explain feasible trade-offs when capacity changes. Management does not assign arbitrary point targets or remove agreed quality work to protect a date. |
| Vendor PM/delivery manager | Own the vendor forecast, contractual obligations, risk response coordination, staffing assumptions, milestone evidence, and escalation. | Commercial reports use team evidence. PM approval does not make unfinished work complete. Keep commercial negotiation out of the team's daily planning. |
| Client sponsor/commercial authority | Authorize changes to baseline obligations, funding, and contractual dates; resolve cross-business conflicts. | Establish signed authority limits. PO preference alone does not authorize a paid change. |
| Client UAT lead/authorized acceptor | Organize acceptance testing and sign the identified deliverable according to the SOW. | May be the PO if formally authorized. Acceptance evidence identifies version, criteria, exceptions, and decision. |

**Example delegation:** The Atlas proxy can clarify labels using an approved glossary and arrange test data. The proxy must refer a new approval role, changed retention rule, or additional integration to the client PO and relevant commercial authority. A clarification affecting estimates is recorded even when the eventual decision costs nothing.

## Events and refinement

The Scrum.org event overview describes planning, daily adaptation, stakeholder inspection, and process improvement within the Sprint. The table turns those purposes into a proposed Atlas routine. The suggested durations are local planning choices for two-week sprints, apart from the Daily Scrum's 15-minute timebox. [Source: Scrum.org event overview](https://www.scrum.org/resources/learn-more-about-each-scrum-event).

| Activity | Participants and suggested cadence | Preparation and useful output |
|---|---|---|
| Sprint | Two weeks for Atlas | A coherent goal, integrated work, feedback, and a current forecast; no extra gap for a separate testing queue. |
| Sprint Planning | Scrum Team; about two hours at sprint start | Bring ordered options, capacity, dependencies, and quality requirements. Leave with a goal, selected work, and an executable plan. Budget risk becomes a visible constraint, not a forced estimate. |
| Daily Scrum | Developers; 15 minutes each working day | Inspect movement toward the goal and adjust work. Hold detailed problem solving afterwards with the needed people. Client status reporting uses a separate summary. |
| Sprint Review | Scrum Team and relevant stakeholders; about 60 minutes | Inspect completed outcomes and discuss changing business conditions. Capture product feedback, assumptions disproved, and ordering implications; route commercial requests to change control. |
| Sprint Retrospective | Scrum Team; about 60 minutes, after the review | Choose a concrete improvement, owner, and follow-up. Include recurring decision latency or rework rather than only team-local issues. |
| Backlog refinement | Relevant team members and PO/proxy; two short sessions per week as needed | Split uncertain work; test examples; identify dependencies and integration risks. Keep time flexible; this is ongoing work rather than an extra formal Scrum event. |

For offshore scheduling, use [Chapter 07](07-offshore-communication.md). Reserve sufficient time for planning and the review/retrospective instead of squeezing every event into a narrow customer call window.

## Readiness, quality, and acceptance

A Definition of Ready is an optional aid. Scrum Alliance distinguishes it from the essential Definition of Done and discusses using it during refinement and planning. Use readiness questions to expose uncertainty, not to create a document approval queue before collaboration can begin. [Source: Scrum Alliance glossary](https://www.scrumalliance.org/glossary/).

### Example Atlas readiness questions

- Can the team explain the user outcome and give at least one concrete success/failure example?
- Is the item linked to baseline scope, an authorized change, or explicitly funded investigation?
- Can the work fit within a sprint after including verification and integration?
- Are required client decisions, access, data, and dependencies available, or is the remaining risk understood?
- Is there a clear way to demonstrate the result and capture evidence?

If an item fails these questions, first split it, clarify it, or propose a bounded investigation. Do not invent a detailed answer just to mark a checklist green.

### Example Atlas Definition of Done

The following is a proposed quality checklist to agree with the team and product stakeholders; it must also meet applicable organizational standards:

- Changes are reviewed, integrated, and built from the controlled repository.
- Relevant automated and exploratory tests pass, including negative and permission paths.
- The slice behaves correctly with existing functionality in the agreed integrated environment.
- Applicable security, accessibility, and performance checks have evidence; material findings are resolved under the quality agreement.
- Operational effects, configuration, migration, monitoring, and rollback needs are covered where relevant.
- User/support documentation and traceable test evidence are current.
- No unresolved defect prevents the slice from meeting its agreed quality and behavioral expectations.

UAT can add business or operational acceptance evidence. It must not become the first occasion on which the team verifies whether its increments work together. Where a test is required to establish the product's quality, plan it into delivery.

### Example acceptance criteria for an Atlas story

**Story:** A requester can track a submitted service request.

| Scenario | Observable acceptance criterion |
|---|---|
| Own request | Given an authenticated requester owns an existing request, when they open its details, then the agreed status and most recent update are displayed. |
| Someone else's request | Given the requester does not have permission to view a request, when they try its identifier, then access is denied without exposing its contents. |
| Status update | Given an authorized agent updates the status, when the requester refreshes the view, then the new state appears under the agreed consistency behavior. |

Specify the permitted statuses and consistency behavior before treating this example as executable acceptance criteria. DoD applies across the increment; these criteria describe this story's behavior.

## Keep the evidence states distinct

| Record/state | Question it answers | Atlas evidence/owner |
|---|---|---|
| DoD evidence | Has the slice met the agreed product quality measures? | Developers link checks to the build and backlog items. |
| Sprint Review feedback | What did stakeholders learn, and what should change next? | PO records feedback and ordering decisions. |
| Contractual deliverable acceptance | Does the submitted package satisfy its specified contract criteria? | Named client acceptor signs the package/version or identifies failures. |
| UAT decision | Do the agreed business scenarios pass for the release candidate? | UAT lead assembles results; authorized acceptor decides. |
| Release authorization | Is this version authorized for production now? | Service owner/change authority records go/no-go conditions. |
| Invoice approval | Has the payment condition been evidenced under the contract? | Commercial/finance authority verifies milestone evidence. |

A good demo is useful evidence, but the SOW determines what is needed for acceptance and payment. Likewise, a pending administrative signature should not be mislabeled as engineering work that never met its quality criteria. Track both states visibly.

## Handle the difficult cases explicitly

**New request during a sprint:** Capture the request and ask the PO and Developers to assess it. Scrum protects the Sprint Goal and quality while allowing scope clarification and renegotiation as learning occurs. [Source: Scrum.org, What is a Sprint?](https://www.scrum.org/node/69415). Apply the commercial workflow separately if the request changes baseline obligations. Do not use an urgent message as spending authorization.

**Story fails its criteria:** Record the unfinished behavior and required work; do not count it as completed scope. Reforecast the affected milestone. Distinguish an implementation defect from genuinely new behavior using the accepted baseline and examples.

**Client PO unavailable:** Use the named deputy and a decision request with options, impact, and required-by time. Work on an unaffected item when feasible. Escalate the blocked dependency; silence is not a decision.

**Client dislikes previously accepted behavior:** Examine whether the implementation matches the accepted version and whether discovery evidence was wrong. Offer a trade-off or priced change when appropriate. The PM should preserve the relationship while making the effect on commitments explicit.

## Sources

Accessed 7 November 2026. Source descriptions establish the framework distinctions; Atlas examples and commercial responsibilities are original practitioner proposals.

1. Ken Schwaber and Jeff Sutherland, [The Scrum Guide](https://scrumguides.org/scrum-guide.html), November 2020. Sections on the Scrum Team, artifacts/commitments, and Increment.
2. Scrum.org, [Learn more about each Scrum Event](https://www.scrum.org/resources/learn-more-about-each-scrum-event), undated learning resource. Event purposes and daily cadence.
3. Scrum Alliance, [Agile Glossary](https://www.scrumalliance.org/glossary/), undated, entries for Definition of Ready and Definition of Done. Distinguishes optional readiness practice from product completion criteria.
4. Scrum.org, [What is a Sprint?](https://www.scrum.org/node/69415), undated learning resource. Sprint Goal protection, quality, and adapting work during a sprint.
