# 08 — Templates and checklists

[Research index](README.md) · [Previous: Offshore communication](07-offshore-communication.md) · [Next: Lessons learned](09-lessons-learned-and-pitfalls.md)

These are original practitioner templates for adaptation to an engagement. Fill in named people, baseline versions, dates, evidence links, and decision authority before use. The sample entries describe the fictional Atlas portal in the [index](README.md); blank fields remain blank until an actual decision is made. Sample terms are proposed commercial arrangements, not claims about what every contract requires.

The templates support the processes in [contracting](02-pre-sales-and-contracting.md), [change management](05-scope-and-change-management.md), and [governance](06-risk-quality-and-governance.md). Their acceptance fields use observable business behavior: Agile Alliance describes acceptance testing through examples or scenarios and stresses customer participation. [S1]

## 1. Statement of Work outline

Use the SOW to make obligations testable. Link annexes by version and precedence; a link to an editable backlog alone leaves the parties without a stable contract reference.

| Section | Fields to complete | Atlas example or completion guidance |
|---|---|---|
| Document control | SOW ID/version: ___; date: ___; parties and authorized signatories: ___; governing agreement: ___ | Delivery SOW v1.0; separate from the $15,000 discovery order. |
| Business objective | Intended users: ___; outcome: ___; success measure and measurement owner: ___ | Allow customers to submit and track service requests. Define the measure before offering a business-outcome guarantee. |
| Deliverables | Deliverable IDs: ___; usable slices: ___; supporting artifacts: ___ | Portal, agreed integrations, deployment package, tests, operations guide, and training. |
| Scope baseline | Backlog export/version: ___; requirements and acceptance annex: ___; nonfunctional requirements: ___ | Baseline B1.0 and criteria AC1.0; 120 illustrative story points for team planning. |
| Boundaries | Exclusions: ___; assumptions and validation dates: ___; dependencies and owners: ___ | Specify supported browsers, identity integration, migration volume, environments, and any excluded native app. |
| Responsibilities | Client PO: ___; vendor PM: ___; proxy delegation: ___; commercial approvers: ___; substitutes: ___ | Client supplies testers, data, and integration access by named dates. |
| Delivery method | Sprint cadence: ___; review arrangements: ___; DoD version: ___; reporting: ___ | Six two-week development sprints; additional preparation, UAT, and release activities. |
| Schedule | Start prerequisites: ___; milestones: ___; client response deadlines: ___; delay handling: ___ | Delivery weeks 1–16; changes to dates require the agreed decision process. |
| Price and payment | Currency: ___; fixed fee: ___; taxes/pass-through costs: ___; invoice evidence: ___; due terms: ___ | $180,000 delivery price; define currency as USD and explicitly identify excluded charges. |
| Acceptance | Authority: ___; review window: ___; rejection evidence: ___; correction/retest: ___; partial acceptance: ___ | A rejection identifies an unmet agreed criterion and evidence; silence handling must be stated explicitly. |
| Change control | Request channel: ___; impact dimensions: ___; scope-swap conditions: ___; approvers: ___; authorization record: ___ | Backlog reprioritization does not alone authorize a price increase. |
| Quality and security | Test evidence: ___; severity definitions: ___; release thresholds: ___; data handling: ___ | Record performance, security, accessibility, and recovery criteria where relevant. |
| Ownership and access | Deliverable/IP treatment: ___; third-party licenses: ___; repository access: ___; secrets transfer: ___ | Identify who receives source, build instructions, and deployment permissions, and when. |
| Release and support | Go-live authority: ___; rollback: ___; warranty start/duration/scope: ___; support channels: ___ | Thirty calendar days of agreed defect correction from production release; maintenance has a separate order. |
| Handover and closure | Training: ___; operations acceptance: ___; open obligations: ___; access revocation: ___ | Final payment evidence includes production deployment and the agreed handover package. |
| Commercial exceptions | Suspension/termination: ___; early-exit settlement: ___; liability: ___; dispute route: ___; jurisdiction: ___ | Identify applicable terms and precedence with the governing agreement. |
| Approval | Approved version: ___; client signatory/date: ___; vendor signatory/date: ___ | Attach the exact annex versions to the signed record. |

### Sample milestone annex

These triggers are illustrative and require the acceptance process in the SOW. A scheduled sprint ending does not by itself satisfy a trigger.

| Milestone | Acceptance evidence | Share | Amount |
|---|---|---:|---:|
| Inception baseline | Agreed scope/criteria/dependencies and inception package | 20% | $36,000 |
| First usable slice, targeted after Sprint 2 | Named slice meets its agreed criteria and is accepted | 20% | $36,000 |
| Second usable slice, targeted after Sprint 4 | Named slice meets its agreed criteria and is accepted | 25% | $45,000 |
| UAT approval | Signed UAT decision for agreed release scope | 25% | $45,000 |
| Production and handover | Deployment evidence and accepted handover checklist | 10% | $18,000 |
| **Total delivery fee** | Separate discovery fee excluded | **100%** | **$180,000** |

## 2. Change request form

Raise this before committing additional contractual work. Use the same form for a proposed scope swap so the removed obligation remains visible.

```text
Change ID and revision:
Raised by / date / decision needed by:
Contract and current baseline version:
Problem or opportunity / business value:
Requested change and observable acceptance criteria:
Classification: clarification / defect / enhancement / swap / disputed
Evidence supporting classification:
Items added / removed / modified:
Options: keep baseline / substitute scope / paid change / defer
Impact: delivery effort, QA, security, integrations, documentation, support
Price change / taxes / payment milestone adjustment:
Schedule change / dependencies / client obligations:
Risk and contingency impact:
Effect on previously accepted work and regression coverage:
Vendor estimate owner / assumptions / confidence / validity date:
Recommendation and reason:
Decision: proposed / approved / rejected / deferred
Client commercial approver / vendor approver / decision date:
Exact approved revision / evidence link:
Implementation authorization / effective baseline version:
Verification and acceptance evidence / closure date:
```

**Example — CR-02, proposed only:** Atlas requests SMS notifications in addition to the agreed email channel. The team sizes it at 12 points for planning; a separate effort and cost assessment proposes a $12,000 fee and one extra two-week development sprint. With early authorization and an otherwise feasible original delivery plan, this branch becomes $192,000, 132 points, and 18 weeks under unchanged UAT/release assumptions. The actual dates, milestone changes, and warranty start must be revised together, including any existing forecast delay. Until signed, B1.0 remains 120 points/$180,000/16 weeks. The quote does not create a $1,000-per-point tariff.

**Swap example:** Remove an unstarted eight-point feature and propose an eight-point substitute. Record integration, testing, operations, and contractual effects before deciding whether the fee and dates can stay unchanged. Equal point counts alone do not approve the swap.

## 3. Sprint report

Publish one concise client report with evidence links. Keep internal cost, reserve, and margin detail in the vendor section unless the engagement expressly shares it. Numbers below are illustrative; status statements in a live report require evidence.

| Field | Copyable prompt | Atlas example at end of Sprint 3 |
|---|---|---|
| Report identity | Project: ___; sprint/dates: ___; author: ___; baseline: ___ | Atlas; Sprint 3; B1.0. |
| Sprint Goal | Goal: ___; outcome and evidence: ___ | Enter the actual goal and demonstration/test evidence; do not infer goal achievement from point totals. |
| Delivery | Selected items: ___; Done items: ___; unfinished items and next decision: ___ | Cumulative Done: 54 of 120 points; this sprint: 16 points after 38 cumulative at Sprint 2. |
| Acceptance | Accepted deliverable/version: ___; pending/rejected criteria: ___; decision due: ___ | Report the client decision separately from the Done count. |
| Scope | Baseline: ___; approved additions/removals: ___; pending requests: ___ | 120 points; CR-02 is proposed and excluded from the baseline. |
| Forecast | Remaining work: ___; throughput assumption: ___; likely completion: ___; confidence: ___ | 66 points remain; mean 18 per sprint suggests 3.67 further sprints, exposing risk to the six-sprint plan. |
| Client commercial status | Fee: ___; approved changes: ___; eligible/invoiced/paid amounts: ___ | $180,000; distinguish evidence-backed milestones from payments actually received. |
| Quality | Defects by severity: ___; failed criteria: ___; test/security evidence: ___ | State the release effect of each blocking defect. |
| Risks/issues | ID: ___; trigger/impact: ___; owner/action/date: ___ | Name the decision that can change the forecast. |
| Decisions required | Decision: ___; authority: ___; deadline: ___; consequence of delay: ___ | Sponsor to choose a recovery option after reviewing scope, dates, and cost. |
| Next sprint | Proposed goal: ___; dependencies: ___; client actions: ___ | Treat the next goal as proposed until planning. |

**Vendor-only cost attachment:** Internal budget at completion is $138,000 including $18,000 contingency. At this example checkpoint, actual cost is $63,000 and bottom-up estimate to complete is $69,000; forecast total cost is $132,000, leaving $6,000 budget headroom and forecast contribution of $48,000 before unmodeled overheads/taxes. A CPI-based forecast is $161,000; explain the evidence for any more optimistic forecast using [the worked calculation](05-scope-and-change-management.md). Neither forecast automatically changes the client fee.

**Example status wording:** “Schedule risk: current output suggests one more development sprint may be needed. The base fee remains $180,000. The team is assessing a scope trade-off and dependency recovery; a decision is due before Sprint 4 planning.” Attach the source data and owner. Do not invent a green status to match the contract date.

## 4. UAT sign-off form

Prepare the acceptance criteria and test ownership early. The sign-off records a business decision about a specific delivery, while the test log records what happened. The format below is an original operational proposal informed by acceptance-testing practice. [S1]

```text
Project / contract / baseline:
UAT cycle and decision ID:
Release/build/version and environment:
Test data and configuration reference:
Agreed acceptance criteria and test plan version:
Client business owner / UAT lead / authorized acceptor:
Execution period / participants:
Results: passed ___ / failed ___ / blocked ___ / not run ___ / total ___
Evidence index and requirement-to-test coverage:
Outstanding defects: ID, severity, impact, workaround, owner, fix/retest date
Outstanding scope or acceptance exclusions:
Decision: accepted / rejected / conditional acceptance if contract permits
Reason and precise scope of decision:
Conditions and who may close them:
Retest required / evidence / decision deadline:
Effect on payment milestone under named contract clause:
Release authorization: separate decision ID / owner / date
Warranty start trigger under contract:
Authorized client signature / date / exact accepted version:
Vendor acknowledgment / owner of remaining actions / date:
Attachments and immutable decision record:
```

**Example:** Atlas UAT executes 40 scenarios: 38 pass and two fail because of minor presentation defects. The client can record conditional acceptance only if the agreed criteria and contract allow it. List both defect IDs, business impact, owners, and correction dates; record whether the $45,000 milestone becomes eligible. This decision does not itself authorize production deployment. Atlas warranty starts at production release under its agreed terms, and pending correction obligations must remain explicit.

Use the agreed blocker threshold. “Minor” should not be decided solely by the vendor, and a waived release risk should not silently rewrite a failed requirement as a passing test.

## 5. Project kickoff checklist

Assign an owner and evidence link to each item. An unchecked prerequisite becomes a dated action, a risk, or a reason to defer affected work.

| Check | Owner to name | Evidence or decision required |
|---|---|---|
| [ ] Contract and discovery/delivery boundary understood | Client and vendor commercial owners | Signed versions, scope exclusions, price, payment and change terms. |
| [ ] Business outcome and first release agreed | Client PO and sponsor | Goal, users, priority outcomes, and success measures. |
| [ ] Decision authority and deputies recorded | Sponsor and vendor engagement lead | Who orders work, accepts deliveries, signs changes, approves invoices, and authorizes release. |
| [ ] Baseline and acceptance references established | Client PO and vendor BA | Versioned backlog, acceptance examples, nonfunctional requirements, and unresolved assumptions. |
| [ ] Estimate and schedule reviewed | Vendor PM and team; client dependency owners | Capacity, dependencies, risk allowance, availability, and forecast assumptions. |
| [ ] Initial backlog ready for planning | PO, BA, Developers | Small first-sprint items, examples, necessary access, and identified dependencies. |
| [ ] Quality and defect rules agreed | Team/QA and client UAT lead | DoD, test strategy, severity definitions, acceptance thresholds, and evidence location. |
| [ ] Environments and access usable | Client IT and vendor technical lead | Repository, CI, test environments, credentials process, data permissions, and integration sandbox. |
| [ ] Client UAT resources booked | Client PO/UAT lead | Named testers, time allocation, test data, and acceptance decision dates. |
| [ ] Cadence and overlap scheduled | Scrum Master and client coordinator | Calendar with time zones/holidays, event purposes, reporting times, and response expectations. |
| [ ] Record locations and access agreed | Vendor PM and client coordinator | Backlog, decisions, documents, tests, approvals, and client-visible report locations. |
| [ ] Risk and escalation arrangements active | Vendor PM and client sponsor | Initial register, named owners, thresholds, and emergency path. |
| [ ] Budget and change controls operational | Commercial owners and vendor PM | Milestone ledger, internal cost forecast, change form, and authorized approval channel. |
| [ ] Release, warranty, and handover planned | Client operations and vendor delivery lead | Go-live owner, support coverage, training, rollback, warranty terms, and maintenance decision date. |
| [ ] Kickoff decisions distributed | Vendor PM | Dated record with owners, deadlines, unresolved decisions, and acknowledgment. |

**Example kickoff exception:** Client integration access is still pending. Record the client IT owner and required date, identify the affected stories, and agree which independent work can proceed. Do not mark the access check complete because an access request was sent.

## Sources

Sources accessed 2026-11-07. The templates, fields, thresholds, and Atlas examples are original proposed working aids, not reproduced standard forms.

- **[S1] Agile Alliance, “Acceptance Testing,” undated glossary entry.** [Read source](https://agilealliance.org/glossary/acceptance-testing/). Supports the use of observable scenarios and customer participation; it does not prescribe contractual sign-off or these templates.
- **[S2] PMI, “Requirements Management: Core Competency for Project and Program Success,” August 2014, public report summary.** [Read source](https://www.pmi.org/learning/thought-leadership/pulse/core-competency-project-program-success). Context for requirements, stakeholder involvement, and scope governance; the report's downloadable full text was not used.
