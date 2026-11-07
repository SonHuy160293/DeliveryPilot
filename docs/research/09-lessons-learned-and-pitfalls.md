# 09 — Lessons learned and pitfalls

[Research index](README.md) · [Previous: Templates and checklists](08-templates-and-checklists.md)

The PM/PO's practical job is to expose consequential decisions while the parties still have useful choices. A report can be accurate about tasks completed and still conceal that the agreed release, acceptance window, or vendor economics no longer works.

The failure patterns and responses below are practitioner synthesis and illustrative scenarios, not measured frequencies or accounts of real Atlas engagements. PMI's 2014 requirements research associates delivery problems with weak requirements management and stakeholder support. Fowler's offshore account emphasizes communication, shared understanding, and integration across locations. Those sources motivate the questions below; the proposed interventions are tailored operating advice. [S1][S2]

## 1. Failure patterns to watch

| Failure mode | Early warning | Likely consequence | Senior PM/PO response |
|---|---|---|---|
| Quoting before testing major assumptions | The estimate has one number and no dependency or exclusion list | The vendor buys uncertainty it cannot control | Price discovery separately; test the expensive unknowns; put unresolved assumptions and consequences in the offer. |
| A baseline that exists only in people's memory | Client and vendor show different backlog exports | Every discussion becomes a dispute about what was included | Establish a dated baseline and criteria annex; reconcile approved changes against it. |
| Treating points as a commercial tariff | A manager multiplies another team's velocity by a standard hourly rate | Forecasts look precise while delivery capacity and costs differ | Estimate commercial effort and capacity explicitly; use the team's point history as one planning input. |
| Accepting a swap because points match | Two eight-point items involve different integrations or regression work | A nominally free substitution creates real cost or risk | Review dependencies, tests, operations, and obligations; sign the removed and added scope together. |
| A client PO without time or authority | Questions remain unanswered through multiple overlap windows | The proxy guesses, work is reworked, and acceptance drifts | Agree a delegate and decision deadline; show schedule consequences; escalate unresolved authority. |
| Sales promises bypass delivery control | A chat message says “we can include that” | Unpriced work enters the sprint without a business trade-off | Log the request and evidence; obtain commercial review before committing it. |
| Vendor proxy silently acts as client sponsor | The BA approves a price or contract-scope change | A later invoice is disputed despite a tidy backlog | Separate delegated product decisions from commercial signature rights. |
| The demo becomes the entire acceptance record | Enthusiastic feedback is reported as an approved milestone | The vendor invoices without evidence of the agreed decision | Record the accepted version, criteria, authority, and decision separately. |
| Quality work accumulates outside sprint reporting | Most items are called complete while integration or security checks wait | Stabilization becomes an unbudgeted development phase | Make the readiness gap visible now; schedule and fund the remaining work; enforce the agreed DoD. |
| UAT has no client capacity | No named testers, data, or acceptance slots | The release slips despite a completed vendor test plan | Book client resources during inception; rehearse representative scenarios early. |
| Scope charts conceal changes | Historical totals are rewritten after new requests | Apparent progress obscures a moving finish line | Preserve scope history and record changes with dates and authority. |
| Green reports survive worsening forecasts | Completed work rises but remaining work and blocked decisions also rise | Steering sees a crisis too late to choose scope or date changes | Show remaining work, assumptions, range, and the decision needed. |
| Contingency is treated as free client scope | Every extra feature is described as “covered by buffer” | Risk funding is spent before identified risks arrive | Track risk allowance and scope changes separately; contingency use does not authorize a new obligation. |
| Offshore savings ignore coordination costs | Constant overnight questions and duplicated analysis | Lower hourly rates fail to produce a lower delivery cost | Budget overlap, BA capacity, integration, and knowledge transfer; track waiting time. |
| Warranty is used as unlimited enhancement capacity | New workflows are filed as defects after go-live | The project cannot close and maintenance economics fail | Classify against the accepted baseline; retain defect obligations and quote enhancements separately. |
| Handover is left to the final day | Only one engineer can deploy or diagnose an incident | The client depends on unavailable vendor staff | Rehearse operations with client staff; verify access, runbooks, recovery, and open actions before closure. |

## 2. Three decisions a senior PM should surface early

### Scenario A — The fixed date is becoming unlikely

Atlas planned six development sprints for a 120-point baseline. After Sprint 3, 54 points are Done. The simple mean is 18 per sprint; 66 remain, suggesting 3.67 additional sprints if the same conditions persist. That is a warning, not a precise completion promise. The calculation and budget context are in [scope and change management](05-scope-and-change-management.md).

Bring the client PO and sponsor concrete choices: remove or defer a coherent low-value slice, resolve a dependency that demonstrably changes throughput, or negotiate a later delivery. Show the acceptance and operational effects of each choice. Adding people is an estimate to assess, with onboarding and coordination cost; it is not an automatic recovery equation.

Keep the existing commitment visible until an authorized revision exists. If the vendor's estimate was wrong and the contract leaves that risk with the vendor, an internal overrun alone is not grounds for a unilateral fee increase. Record the forecast honestly and resolve obligations through the agreed commercial route.

### Scenario B — A late UAT request is disputed

During Atlas UAT, the client expects SMS notifications, while the baseline specifies email. Compare the request with the baseline, examples, and acceptance criteria. An agreed behavior that fails needs defect correction; a new behavior needs a scope decision. If the evidence is ambiguous, record the ambiguity and seek a joint resolution instead of editing the original criteria after the event.

For an actual enhancement, CR-02 illustrates a proposed $12,000 and one extra two-week sprint. Its $192,000/18-week branch assumes an early decision and an otherwise feasible original delivery plan. A request arriving during UAT needs a fresh assessment of implementation, regression, repeat UAT, and any existing delay; it cannot inherit that date mechanically. The [change form](08-templates-and-checklists.md) also records payment and warranty-date effects. Until authorization, preserve the original $180,000/16-week baseline and the open decision.

### Scenario C — Production is live but ownership is incomplete

The portal works, yet client operations cannot restore a backup and the only deployment credentials belong to a departing vendor engineer. A successful demonstration did not resolve operational ownership.

List the unfulfilled handover criteria, assign a vendor owner and client verifier, rehearse the missing operation, and retain evidence. Apply the final milestone's actual acceptance terms. Under Atlas's example agreement, the 30-calendar-day warranty starts at production release; a delayed handover does not silently reset that clock or erase an outstanding delivery obligation. Any different arrangement must be expressly agreed.

## 3. Habits that prevent recurring surprises

At the start of each sprint, check which client decision, external dependency, and quality gap could invalidate the forecast. A dependency without an owner and needed-by date is not a plan. Ask for an example of the intended business behavior before estimating a vague acceptance criterion.

At each review, inspect actual usable work and record feedback. Follow through with the separate commercial decisions required by the engagement. In the report, put a named action beside a risk; “client to confirm” is incomplete without an accountable person and date.

Before a steering meeting, reconcile the scope ledger, financial forecast, acceptance ledger, and release calendar. If they disagree, explain the discrepancy in the decision paper. A new baseline should identify who approved what and when, while retaining the history needed to understand the variance.

Before closure, have the recipient demonstrate that they can operate the system, find the current documentation, access the agreed assets, and route support. Use observed ability and evidence to close handover items.

## 4. Turn lessons into an improvement

Use retrospectives for changes the team can make during delivery. Use an engagement review to capture wider commercial and client/vendor lessons after a major milestone or closure. Keep facts separate from interpretations and avoid turning the review into a search for someone to blame.

| Review field | Example proposed improvement |
|---|---|
| Observable event | Two stories waited three business days for an integration decision. |
| Delivery/business effect | The team switched work and the acceptance slot had to move. |
| Contributing conditions | Client IT had no named deputy; escalation began only after the deadline. |
| Change to working agreement | Name a deputy and flag missing decisions one business day before the needed-by date. |
| Owner and due date | Client PO and vendor PM before next Sprint Planning. |
| Evidence of improvement | Compare decision waiting time over the next two sprints. |
| Wider reuse | Add the deputy/decision-date fields to future kickoff checklists. |

Do not turn one project's numbers into a universal rule. Record context: team familiarity, integration complexity, client availability, and what the contract actually required. A useful lesson changes a future decision or operating habit and has an owner who checks whether it helped.

## Sources

Sources accessed 2026-11-07. The examples and failure-response table are original practitioner synthesis; no stakeholder interviews or real Atlas project results are claimed.

- **[S1] PMI, “Requirements Management: Core Competency for Project and Program Success,” August 2014, public report summary.** [Read source](https://www.pmi.org/learning/thought-leadership/pulse/core-competency-project-program-success). Supports the relevance of requirements management, communication, and stakeholder support. Historical research provides context, not a current failure-rate estimate for fixed-price Scrum projects.
- **[S2] Martin Fowler, “Using an Agile Software Process with Offshore Development,” 18 July 2006; author notes a 2011 revisit.** [Read source](https://martinfowler.com/articles/agileOffshore.html). First-hand Thoughtworks experience concerning offshore collaboration and integration; its organizational context and age limit generalization.
- **[S3] Agile Alliance, “Acceptance Testing,” undated glossary entry.** [Read source](https://agilealliance.org/glossary/acceptance-testing/). Background for the observable-behavior approach to acceptance used in the guide; contractual authority comes from the engagement agreement.
