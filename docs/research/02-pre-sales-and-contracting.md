# 02 — Pre-sales and contracting

[Research index](README.md) · Previous: [Overview](01-overview.md) · Next: [SDLC phases](03-sdlc-phases.md)

**Evidence:** Research-supported estimating and contracting concepts, with proposed operating practices and fictional Atlas numbers. Sources checked 2026-11-07.

## Make uncertainty a priced decision

Pre-sales should establish what the client can accept and what the vendor can responsibly promise. The proposal needs an estimate basis, a definition of the purchased outcome, and a route for dealing with new information. A sales target alone is not an estimate.

PMI's fixed-price practitioner guidance emphasizes explicit scope and acceptance criteria, assumptions, risk allowances, and controlled contract changes. The delivery team should participate before the price becomes binding. ([Lowden and Thornton, 2015](https://www.pmi.org/learning/library/challenges-fixed-price-contracts-9640))

## Discovery and inception

In the Atlas example, a two-week discovery costs $15,000 under a separate agreement. It produces enough evidence for a build decision. The subsequent $180,000 delivery engagement includes a one-week inception period to establish the operating baseline, access, environments, and initial delivery plan. Inception confirms and prepares the contracted work; it is not an unlimited opportunity to redefine what was sold.

| Discovery activity | Client responsibility | Vendor responsibility | Evidence for a delivery quote |
|---|---|---|---|
| Define business outcome and users | Sponsor and PO confirm value, priorities, and mandatory dates | BA facilitates workflow and outcome mapping | Objectives, user journeys, launch boundary |
| Validate difficult integrations | Supply owners, access, API documentation, and realistic sample data | Engineers run a timeboxed feasibility spike | Observed behavior, constraints, remaining unknowns |
| Define acceptance and quality | Identify business approver, UAT users, and operational needs | QA and technical lead propose measurable tests | Acceptance matrix and nonfunctional targets |
| Establish delivery scope | PO decides essential versus optional outcomes | Team decomposes and estimates the backlog | Versioned baseline and estimate range |
| Review commercial exposure | Procurement confirms terms and approval authority | PM and commercial lead price retained risks | Assumptions, exclusions, risk allowance, draft SOW |

At discovery exit, decide to proceed, narrow scope, investigate a specific unknown, change the commercial model, or stop. Do not disguise an unresolved feasibility question as a low-confidence fixed estimate. The idea of using a pilot before a more constrained commitment also appears in Leybourn's contracting discussion. ([PMI paper](https://www.pmi.org/learning/library/measuring-value-10209))

## Estimate planning capacity and commercial cost separately

Story points express relative effort for a particular team; a fixed “one point equals eight hours” conversion creates misleading precision. Mike Cohn discusses this distinction and the use of observed delivery data for forecasting. ([Mountain Goat Software](https://www.mountaingoatsoftware.com/agile/dont-equate-story-points-to-hours))

Use the team's sizing and delivery history to assess a schedule range. Use a staffing and cost model to price the work. Reconcile the two estimates before quoting.

| Estimate view | Inputs | Output and limitation |
|---|---|---|
| Backlog forecast | Relative sizes, dependency order, comparable team history, quality scope | Plausible sprint range; confidence falls when team, work, or environment changes |
| Effort estimate | Role-level tasks or work packages, review/testing effort, assumptions | Hours or days with ranges; include collaboration and rework likely within scope |
| Capacity plan | Availability, leave, shared specialists, client turnaround | Feasible staffed calendar; paid hours are not all feature implementation time |
| Cost model | Loaded labor costs, required services, environments, travel if included | Vendor delivery cost; differs from customer price |
| Commercial price | Delivery cost, retained risk, contribution target, terms | Quote with validity period, inclusions, exclusions, and approval conditions |

**Atlas illustration:** A 120-point backlog and this team's observed 18–24 points per sprint on comparable work imply roughly five to seven sprints before considering particular dependencies. Six sprints is a working plan requiring validation. It is not a promise to “produce 20 points” every sprint. A new team without relevant history should use wider scenarios and targeted discovery rather than borrow another team's velocity as a commitment.

For a simple cost illustration, 2,400 estimated loaded labor hours at a blended $45 cost produce $108,000. Add $12,000 of included environment/tooling and nonlabor allowances to obtain the $120,000 base. These are fictional estimating inputs, not recommended market rates. The labor estimate covers analysis, engineering, QA, management, UAT support, release/handover, and a warranty allowance; it must be reconciled to role availability before proposal approval.

## Build a baseline that can be tested

The commercial baseline is a versioned description of the purchase, connected to backlog items and acceptance evidence. It should allow implementation details to emerge without making the agreed outcome unknowable.

| Baseline component | Atlas example |
|---|---|
| Outcomes and user roles | Service request submission, staff processing, requester status tracking |
| Included capabilities | Agreed portal workflows, notifications, reporting, and named integration |
| Quality and operating envelope | Explicit response-time/load conditions, accessibility/security criteria, supported environments |
| Data and migration | Named data sets, formats, volumes, cleansing owner, reconciliation rules |
| Deliverables | Software, test evidence, deployment scripts, runbooks, training, source/access transfer |
| Acceptance | Version-specific criteria, test evidence, authorized approver, review and retest process |
| Constraints and dependencies | Client identity provider, integration availability, access dates, UAT availability |
| Commercial mapping | Deliverable IDs linked to milestones, inclusions/exclusions, change authority |

Detail the imminent sprint work sufficiently for execution and retain measurable boundaries for later work. Record open decisions with an owner, due date, and effect if unresolved. A label such as “standard reporting” is too vague to price or accept.

## Assumptions and exclusions that are operational

| Statement | Evidence or action | Treatment if it fails |
|---|---|---|
| Client provides integration sandbox before inception exit | Named owner demonstrates access and representative calls | Log impact, use agreed mitigation, seek a documented schedule/commercial decision |
| Client returns routine requirement decisions within two business days | PO and deputy named; decision log timestamps requests | Escalate before blocked work consumes the next sprint |
| Migration includes one agreed source format and volume envelope | Client supplies sample and profile | Assess changed volume/schema through change control |
| Native mobile applications and additional languages are excluded | Exclusion linked to roadmap alternatives | Price separately if requested |
| External licenses and transaction charges are pass-through | Named payer and approval mechanism | Obtain authorization before incurring commitments |

An assumption is not an automatic right to charge more. The SOW must explain notification, evidence, mitigation, decision authority, and any agreed relief. Avoid making exclusions so broad that the advertised outcome cannot be delivered.

## Price, contingency, and contribution

Contingency addresses identified uncertainty; management reserve addresses unforeseen work within the authorized project and uses a separate management decision. They need different control rules. PMI's reserve model places contingency within the cost baseline and management reserve outside it. ([Shrivastava, 2014](https://www.pmi.org/learning/library/model-risk-contingency-reserve-9310))

| Atlas delivery price component | Amount | Meaning |
|---|---:|---|
| Base delivery cost | $120,000 | Planned work, including QA, release, and warranty allowance |
| Identified-risk contingency | $18,000 | Reserved response/uncertainty allowance; 15% of base in this example |
| Vendor cost baseline | **$138,000** | Base plus contingency |
| Planned commercial contribution | $42,000 | Amount remaining if the entire cost baseline is spent |
| Client fixed delivery price | **$180,000** | Agreed price excluding taxes and pass-through costs |

No separate management reserve is included in this simplified example. Any such provision must be explicitly identified by vendor management; contingency and contribution must not be counted twice. Contribution is not automatically net profit because company-level overhead and financing treatment depend on the accounting model.

The $18,000 allowance needs a risk analysis; 15% is an illustration, not an industry benchmark. For example, a 30% probability of a $20,000 integration remediation yields $6,000 expected exposure. Add other identified risks and examine combined downside scenarios: one expected-value total does not establish a confidence level or cover every possible loss. Track reserve use against risk IDs and authorization.

## Milestones that release cash against evidence

Discovery is invoiced separately under its $15,000 agreement. The delivery milestones below total $180,000; their dates are targets subject to the actual acceptance evidence.

| Delivery milestone | Planned point | Required evidence | Payment |
|---|---|---|---:|
| Inception baseline accepted | End of delivery week 1 | Baseline, acceptance plan, responsibilities, and readiness evidence | $36,000 / 20% |
| First usable slice accepted | After sprint 2, week 5 | Agreed request-submission slice with test evidence | $36,000 / 20% |
| Second usable slice accepted | After sprint 4, week 9 | Agreed staff-processing slice and integration evidence | $45,000 / 25% |
| UAT approved | End of week 15 | Identified build, executed tests, accepted defect disposition | $45,000 / 25% |
| Production release and handover complete | Week 16 | Release verification and accepted handover checklist | $18,000 / 10% |
| **Total** | | | **$180,000 / 100%** |

Proposed Atlas terms: the client has five business days after a complete milestone submission to accept it or identify failures against the agreed criteria. The vendor corrects attributable defects and resubmits. The agreement specifies how retest windows work. Silence triggers escalation in this example; it does not automatically equal acceptance. Undisputed invoices are due within 15 calendar days of issue after milestone acceptance.

A Sprint Review may provide evidence for a milestone; attending the event is not itself an invoice authorization. Define partial acceptance, disputed amounts, holdbacks if any, and the treatment of late feedback before work starts. Warranty obligations continue for the agreed period even if the release milestone is already paid.

## A practical change clause and negotiation checklist

**Illustrative clause structure:** “Either party may request a change referencing the affected baseline version. The vendor will assess effects on deliverables, acceptance, price, schedule, dependencies, and warranty. Work on the requested change begins only after the named commercial authorities approve a change order, except for separately defined emergency actions. An approved scope substitution may retain the existing price when the parties agree the full impact is equivalent. The approved record updates the applicable baseline and milestone schedule.”

Complete the clause with assessment turnaround, whether substantial analysis is chargeable, rejected-change treatment, emergency limits, dispute escalation, and authorized signatories. Routine clarification stays with the PO within delegated authority; removing a contractual obligation requires the appropriate commercial approval.

Before signature, walk through four scenarios together: a missed estimate, a new client feature, a late client dependency, and a disputed UAT result. Both parties should reach the same conclusion about who decides, who pays, what evidence is needed, and whether the date changes. Also settle intellectual property and access transfer, confidentiality/data responsibilities, termination settlement, warranty exclusions, and the separate maintenance scope. Use the [SOW and change request templates](08-templates-and-checklists.md) to record the result.

## Sources

- George Lowden and John Thornton. [The special challenges of project management under fixed-price contracts](https://www.pmi.org/learning/library/challenges-fixed-price-contracts-9640), PMI Global Congress paper, 11 May 2015. Scope, assumptions, acceptance, and fixed-price cost exposure. Accessed 2026-11-07.
- Evan Leybourn. [How much does this cost?](https://www.pmi.org/learning/library/measuring-value-10209), PMI Global Congress paper, 13 May 2016. Constrained delivery and pilot approaches. Accessed 2026-11-07.
- Mike Cohn / Mountain Goat Software. [Don't Equate Story Points to Hours](https://www.mountaingoatsoftware.com/agile/dont-equate-story-points-to-hours), updated 6 December 2024. Relative estimates and limits of fixed points-to-hours mappings. Accessed 2026-11-07.
- Narendra K. Shrivastava. [A model to develop and use risk contingency reserve](https://www.pmi.org/learning/library/model-risk-contingency-reserve-9310), PMI Global Congress paper, 26 October 2014. Reserve distinctions and expected monetary value. Accessed 2026-11-07.

The pricing, milestone terms, risk amounts, and clause text in this chapter are original illustrative proposals for Atlas. The PMI papers are publicly accessible practitioner papers; no inaccessible PMI standard is presented as having been consulted.
