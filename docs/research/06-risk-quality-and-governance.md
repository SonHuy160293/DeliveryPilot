# 06 — Risk, quality, and governance

[Research index](README.md) · [Previous: scope and change management](05-scope-and-change-management.md) · [Next: offshore communication](07-offshore-communication.md)

**Research-supported guidance; the sample register, thresholds, and meeting structure are practitioner proposals.** Source access date: 2026-11-07.

A fixed price concentrates attention on overruns, but the useful management question is broader: what could stop the client receiving an acceptable, supportable product, and who can act before that happens? Connect each material risk to an owner, an observable trigger, a response, and a decision date. Connect quality reports to the same delivery commitments.

## Maintain a register that drives action

PMI's published Nokia Siemens case describes iterative risk identification, named owners, triggers, responses, and reassessment. Its organization-specific scoring scheme is an example, not a mandatory scale for every project. [Lavanya and Malarvizhi, PMI](https://www.pmi.org/learning/library/risk-analysis-project-management-7070)

For Atlas, use these fields: ID; cause/event/consequence statement; affected commitment; likelihood and impact; owner; trigger; prevention action and due date; contingency action; response cost and funding authority; residual exposure; review date; status; and linked evidence/issues. Define likelihood bands and impact thresholds together at kickoff. Keep a short explanation beside the score so a red cell has a usable meaning.

| ID and risk statement | Initial assessment | Trigger and owner | Prevention and contingency |
|---|---|---|---|
| R-01: Client API credentials may arrive late, delaying the integrated request workflow. | Medium likelihood; potentially one sprint of schedule impact | No working sandbox by five business days before Sprint 2 planning. Client integration owner. | Test access during inception. Vendor technical lead prepares a mock for isolated testing; PM assesses resequencing and any contractual notice if access fails. |
| R-02: Slow client policy decisions may cause rework in authorization rules. | High likelihood; up to $6,000 vendor exposure | Two priority questions exceed the agreed response window. Client PO. | Book decision sessions and define a deputy. If triggered, escalate a written options paper; defer dependent work where possible. |
| R-03: Only one vendor engineer understands deployment, threatening release continuity. | Medium likelihood; two-day recovery risk | No second person has completed deployment rehearsal by Sprint 4. Vendor technical lead. | Pair on infrastructure work and maintain a runbook. Use the trained backup and rehearse restoration if the primary becomes unavailable. |
| R-04: Representative UAT users may be unavailable, delaying the acceptance milestone. | Medium likelihood; two-week acceptance exposure | Named testers and protected calendars are missing at Sprint 4 review. Client business owner. | Nominate users during inception and review scenarios each sprint. Sponsor obtains coverage or agrees a revised UAT plan and commercial impact. |
| R-05: Sensitive customer data may enter a test environment without suitable controls. | Low likelihood; high confidentiality impact | Real data appears in an unapproved dataset or access log. Client data owner. | Use synthetic data and approve any exceptional import. Vendor security lead restricts access and contains exposure through the agreed incident process if triggered. |

Assign one named risk owner even where several people perform response actions. The PM maintains the overall register and checks overdue actions; the person best able to influence the cause owns the response. Record who pays for prevention, who can authorize contingency use, and which contract assumptions allocate the consequences.

A trigger can be an early warning without the event having occurred. When a risk does occur, create or link an issue with a resolution owner and deadline, then reassess remaining risk. Keep the history instead of deleting the risk because it became inconvenient.

### Price uncertainty without double counting it

Atlas's $18,000 contingency is part of its $138,000 vendor cost budget. An illustrative $6,000 impact with a 40% probability has a $2,400 expected exposure. That arithmetic can inform discussion; it does not prove the right reserve, especially where risks are correlated or one event threatens the whole release. Do not add the same rework allowance to story estimates, contingency, and the forecast again.

At the Sprint 3 snapshot, the $132,000 cost forecast implies $12,000 above base costs and $6,000 budget headroom. It does not establish which risks consumed money. Reconcile the risk-response ledger with actual costs and the remaining forecast. See [the financial example](05-scope-and-change-management.md) for the full calculation.

## Build quality into the delivery plan

Fowler describes continuous integration as frequent integration backed by automated build/test checks to expose integration problems quickly. NIST SSDF recommends incorporating security practices into the SDLC and provides a shared vocabulary for purchaser/supplier discussions. The following Atlas quality plan applies these ideas through explicit deliverables and ownership. [Fowler](https://martinfowler.com/articles/continuousIntegration.html), [NIST SSDF](https://csrc.nist.gov/pubs/sp/800/218/final)

| Timing | Proposed quality work | Evidence and responsibility |
|---|---|---|
| Discovery/inception | Agree critical user journeys, nonfunctional targets, data handling, test environments, defect severity, and acceptance evidence. | Client PO/business owner supplies acceptance examples; vendor QA lead drafts strategy; technical/security leads review risks. |
| Each backlog refinement | Review examples, boundary cases, dependencies, testability, and security implications before forecasting the work. | Story criteria, test notes, dependency records; client PO and vendor BA/Developers/QA collaborate. |
| Each code change | Review code; run appropriate unit, integration, and contract checks; inspect dependencies and sensitive-data handling. | Build results and review links tied to the change; Developers own implementation quality with QA support. |
| Each sprint | Exercise end-to-end paths, exploratory cases, relevant regression, accessibility, and incremental performance checks. | Versioned test results and defect dispositions; QA coordinates, Developers correct, client users provide feedback. |
| Stabilization/UAT | Rehearse full business journeys, migration and recovery; repeat relevant regression; investigate remaining acceptance gaps. | Vendor supplies tested release candidate and evidence; client business users execute UAT and authorized client owner decides acceptance. |
| Release/warranty | Verify deployment, monitoring, access transfer, rollback, known issues, and defect response. | Joint readiness record; client operations accepts operational ownership; vendor supports agreed warranty obligations. |

QA work is funded in the base delivery estimate and performed throughout the sprints. Stabilization is an opportunity to validate the complete release and operational environment; it should not become a hidden queue of unfinished sprint testing. A missed DoD check remains unfinished work and stays visible in the forecast.

### Agree defect treatment before the first disputed defect

An illustrative severity scheme is: **critical** for a security/data-loss incident or unusable essential service; **major** for a mandatory business journey blocked without an acceptable workaround; **minor** for a limited defect with a viable workaround. Priority is the order of repair and can differ from severity. QA records impact and reproduction evidence; the client PO provides business urgency; technical leads assess recovery and regression.

For Atlas, propose no unresolved critical or major defects at release. A minor defect can be deferred only with an authorized, versioned record specifying impact, workaround, owner, repair date, and whether it remains covered by warranty. An exception to a release decision does not erase the defect or change the DoD retroactively. Define response and restoration expectations separately; “acknowledge within four business hours” does not promise a repair in four hours.

## Give each decision an owner

PMI governance guidance emphasizes tailoring decision responsibilities, communication, assurance, and control to the project. Atlas's proposed decision matrix makes that operational without requiring every question to reach a steering committee. [Alie, PMI](https://www.pmi.org/learning/library/project-governance-critical-success-9945)

| Decision | Decision authority | Evidence/input |
|---|---|---|
| Product priority and business clarification within agreed scope | Client PO; vendor proxy only within written delegation | Value, criteria, dependencies, and team estimates |
| Technical implementation and daily plan | Developers within architectural/security constraints | Engineering evidence and delivery risks |
| Internal staffing and contingency allocation within delegated limits | Vendor delivery manager/commercial owner | Cost forecast, risk-response plan, and impact on commitments |
| Contract scope, price, milestone, or liability amendment | Authorized client and vendor commercial representatives | Change request and impact analysis |
| UAT acceptance | Named client acceptance authority | Tested version, criteria, results, and defect dispositions |
| Production release | Named client release/operations authority, supported by vendor readiness recommendation | Deployment, rollback, monitoring, business readiness, and acceptance prerequisites |

The steering committee resolves cross-organization priorities and decisions beyond delegated authority. For Atlas, include the client sponsor, client PO, vendor engagement/delivery lead, and commercial representatives when needed. Invite security, operations, or integration owners for relevant items. Require the people authorized for the decision to attend or delegate; record decisions, conditions, owners, and deadlines.

## Reporting cadence and escalation

| Cadence | Participants and purpose | Expected output |
|---|---|---|
| Daily working visibility | Team; PO/PM receive or join relevant follow-up | Blockers, aging decisions, failed checks, and immediate action owners |
| Weekly delivery review, 30 minutes | Client PO, vendor PM, technical/QA leads | Forecast changes, top risks/issues, client actions, scope decisions, and commercial exceptions |
| Each sprint | Scrum participants and relevant stakeholders | Product feedback, updated backlog/forecast, quality evidence, and improvement actions; contractual acceptance is separately recorded where required |
| Every four weeks and by exception | Steering committee | Decisions on material trade-offs, commercial exposure, milestones, and unresolved escalations |
| Release gate | Client business/operations authorities and vendor leads | Go/no-go evidence, outstanding conditions, and accountable owners |

Use one brief report: reporting date and baseline version; delivery outcome; next milestone and confidence; approved/pending scope movement; client financial position; quality; top three risks/issues; and decisions required with deadlines. Vendor cost/margin details can remain in an internal companion view according to the engagement's disclosure agreement. A green overall status must not hide a red release prerequisite.

Agree escalation thresholds at kickoff. Proposed Atlas triggers are a forecast milestone slip exceeding five business days, cost EAC exceeding the $138,000 vendor budget, a critical defect, or a decision blocking the team beyond two business days. These thresholds are illustrative and need a stated business-hours calendar.

The route is **issue owner → vendor PM/client PO → delivery leads and client sponsor → authorized commercial decision-makers or steering committee**. Security and production incidents use the incident route immediately rather than waiting for the next meeting. The escalation note contains facts, baseline impact, options, recommendation, decision owner, and last useful decision time. When the decision arrives, confirm who executes it and when the forecast and records will be updated.

## Sources

1. N. Lavanya and T. Malarvizhi. [Risk analysis and management: a vital key to effective project management](https://www.pmi.org/learning/library/risk-analysis-project-management-7070), PMI Global Congress, 3 March 2008. Used for iterative risk management, ownership, triggers, and response monitoring; sample risk entries are original.
2. Salina Sandra Alie. [Project governance: #1 critical success factor](https://www.pmi.org/learning/library/project-governance-critical-success-9945), PMI Global Congress, 10 October 2015. Used for governance tailoring, decision responsibilities, and reporting/control principles.
3. Martin Fowler. [Continuous Integration](https://martinfowler.com/articles/continuousIntegration.html), revised 18 January 2024. Used for frequent integration and automated verification.
4. Murugiah Souppaya, Karen Scarfone, and Donna Dodson. [NIST SP 800-218: Secure Software Development Framework, Version 1.1](https://csrc.nist.gov/pubs/sp/800/218/final), February 2022. Publication abstract consulted for integrating security practices into the SDLC and supplier communication; no certification or compliance claim is made.

All sources accessed 2026-11-07. The register, quality matrix, severity rules, meeting cadence, and escalation thresholds are an illustrative operating design rather than quoted framework requirements.
