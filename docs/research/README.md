# Research: Agile Scrum outsourcing under a fixed-price contract

This guide explains how a client and software vendor can run iterative delivery while keeping scope, acceptance, payment, and risk decisions explicit. It is written for project managers, Product Owners, delivery leads, and engagement managers who need a usable operating approach from pre-sales to maintenance.

**Research date:** 2026-11-07. **Evidence status:** Research-supported guidance and clearly identified practitioner proposals. The user approved the research scope in this conversation; the findings are research inputs, not approved DeliveryPilot requirements or evidence of implemented product capabilities.

## Reading order

| File | What it helps you decide or do |
|---|---|
| [00 — Research plan](00-research-plan.md) | Review the approved scope, questions, method, and completion criteria. |
| [01 — Overview](01-overview.md) | Compare fixed-price, sprint/phase pricing, capped T&M, early exit, and scope-swapping arrangements. |
| [02 — Pre-sales and contracting](02-pre-sales-and-contracting.md) | Turn discovery evidence into an estimate, baseline, price, payment schedule, and change clause. |
| [03 — SDLC phases](03-sdlc-phases.md) | Follow all seven phases with activities, deliverables, entry/exit criteria, and client/vendor responsibilities. |
| [04 — Scrum operating model](04-scrum-operating-model.md) | Set roles, events, proxy authority, readiness, quality, and acceptance arrangements. |
| [05 — Scope and change management](05-scope-and-change-management.md) | Classify requests, assess swaps, approve changes, and read burn-up and earned-value examples. |
| [06 — Risk, quality, and governance](06-risk-quality-and-governance.md) | Use a risk register, continuous QA plan, decision matrix, reporting cadence, and escalation route. |
| [07 — Offshore communication](07-offshore-communication.md) | Plan time-zone overlap, records, handoffs, status reports, demos, and acceptance meetings. |
| [08 — Templates and checklists](08-templates-and-checklists.md) | Copy a SOW outline, change form, sprint report, UAT sign-off, and kickoff checklist. |
| [09 — Lessons learned and pitfalls](09-lessons-learned-and-pitfalls.md) | Recognize warning signs and make recovery decisions before commitments become disputes. |
| [README — This index](README.md) | Find topics, shared example assumptions, terminology, and source conventions. |

Read 01–04 when establishing an engagement, use 05–07 during delivery, and adapt 08 with the authorized client/vendor representatives. Use 09 during proposal reviews, retrospectives, and engagement closure.

## Shared fictional engagement: Atlas service portal

Atlas is an invented outsourcing example used to keep the documents' arithmetic and decisions consistent. Its amounts, durations, rates, and thresholds are illustrative, not market benchmarks or recommended contractual defaults. Its baseline covers request submission, staff processing, status tracking, email notifications, and agreed reporting/integration scope.

| Assumption | Value used throughout the guide |
|---|---|
| Currency and exclusions | USD; prices exclude taxes and pass-through costs. |
| Discovery | Separate two-week engagement for $15,000. |
| Delivery contract | Fixed price of $180,000; baseline B1.0; illustrative initial sizing of 120 story points. |
| Delivery schedule | Week 1 inception; weeks 2–13 six two-week development sprints; weeks 14–15 stabilization/UAT; week 16 release and handover. |
| Warranty and maintenance | Thirty calendar days from production release for agreed defect remedies; maintenance contracted separately. |
| Estimate basis | 2,400 loaded labor hours across delivery roles/phases and warranty allowance at an illustrative $45 cost, plus $12,000 included nonlabor costs = $120,000 base. Role allocation and calendar feasibility must be reconciled before bidding. |
| Vendor cost budget | $120,000 base plus $18,000 contingency = $138,000. No separate management reserve in this example. |
| Planned contribution | $42,000 if the entire $138,000 cost budget is spent; contribution is not automatically net profit. |
| Payment milestones | $36,000 inception; $36,000 accepted slice after Sprint 2; $45,000 accepted slice after Sprint 4; $45,000 UAT approval; $18,000 production and handover. |
| Example commercial terms | Five business days to review a complete submission; silence escalates. Undisputed invoices due 15 calendar days after issue following acceptance. |
| Authority | Client PO owns product decisions; vendor BA acts within explicit proxy delegation; named client/vendor commercial authorities approve contract amendments. |

The schedule describes a baseline, not a claim that the example has been delivered. The chapters examine separate decision scenarios against it:

- **Sprint 3 forecast:** Cumulative Done work is 54 points. Vendor actual cost is $63,000; bottom-up remaining cost is $69,000, giving a $132,000 forecast. Compare it with the more pessimistic $161,000 CPI-based forecast in Chapter 05.
- **Possible scope swap:** An unstarted eight-point CSV export is traded for an eight-point saved search only after the full commercial and technical impact is agreed.
- **Proposed CR-02:** SMS notifications add an estimated 12 points, $12,000, and one two-week sprint. The $192,000/132-point/18-week branch assumes the original delivery plan remains feasible and the change is authorized early enough. It does not absorb unrelated delay or grant approval; late requests require a fresh schedule assessment.
- **Early-exit alternative:** Chapter 01 evaluates a different contract clause; that clause is not assumed to exist in Atlas's baseline.

## Terms used in the guide

| Term | Meaning here |
|---|---|
| SOW | Statement of Work defining the purchased obligations and their commercial terms. |
| Baseline | Identified version of agreed scope, criteria, cost/schedule commitments, and related assumptions. |
| PO / proxy | Product Owner / a person delegated specified supporting work; the delegation must identify authority boundaries. |
| DoR / DoD | Definition of Ready / Definition of Done; their different roles are explained in Chapter 04. |
| UAT | User acceptance testing against agreed business scenarios for an identified release candidate. |
| CR | Change request; a request becomes an authorized contract change only through the agreed decision process. |
| T&M | Time and materials, with the actual payment basis and any cap defined in the agreement. |
| BAC / PV / EV / AC | Budget at completion / planned value / earned value / actual cost in the vendor cost example. |
| ETC / EAC | Estimate to complete / estimate at completion; label the assumptions behind each forecast. |

Use Chapter 04's evidence-state table when discussing completion. A test result, Sprint Review, contractual acceptance, release authorization, and payment decision answer different questions.

## How to use the evidence

Each topic ends with verified source links, publication information where available, and access dates. Nearby citations connect the principal sourced claims to that list. Framework descriptions, historical industry proposals, and original operating recommendations are identified separately. Historical articles provide context; they do not establish current market prices, universal contract terms, or measured outcomes for Atlas.

The research uses the official Scrum Guide, public PMI guidance and practitioner papers, and attributable industry sources. It does not claim to have consulted inaccessible PMI standards. The reusable forms and financial calculations are original examples; adapt their assumptions and authority fields to the actual engagement.

## Sources

Accessed 2026-11-07. The index summarizes this collection; detailed source scope and evidence are recorded in the linked chapters.

- Ken Schwaber and Jeff Sutherland, [The Scrum Guide](https://scrumguides.org/scrum-guide.html), November 2020 — framework reference used in Chapters 01 and 04.
- George Lowden and John Thornton, [The special challenges of project management under fixed-price contracts](https://www.pmi.org/learning/library/challenges-fixed-price-contracts-9640), PMI Global Congress, 11 May 2015 — practitioner contracting reference used in Chapters 01 and 02.
- Jeff Sutherland, [Agile Contracts: Money for Nothing and Your Change for Free](https://jeffsutherland.com/scrum/2008/10/agile-contracts-money-for-nothing-and.html), 25 October 2008 — original historical contracting proposal examined in Chapter 01.
- [Approved research plan](00-research-plan.md) and [repository authoring policy](../authoring.md) — scope provenance and the boundary between research and adopted product requirements.
