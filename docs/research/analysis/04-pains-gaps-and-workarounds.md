---
id: RESEARCH-DP-ANALYSIS-04
title: "04 — Pains, information gaps and workarounds"
status: proposed
---

# 04 — Pains, information gaps and workarounds

[Analysis index](README.md) · [Previous: Phase needs](03-phase-needs-and-journeys.md) · [Next: Product flow](05-product-sdlc-flow.md)

## Prioritization method

The ranking is **AI-inference**, not a measured survey. Severity estimates the consequence when a problem occurs: critical = material contractual/financial exposure; high = major delay, rework or lost decision confidence; medium = coordination cost with a viable workaround. Frequency is a **hypothesis** about recurrence within an engagement: repeated, episodic, or concentrated at transitions. Confidence rates the relevance of the evidence, not the probability of an event. No source here establishes population frequency for fixed-price Scrum outsourcing.

| Rank / pain | Severity | Frequency hypothesis | Evidence confidence / basis | Failure mechanism and signals to validate |
|---|---|---|---|---|
| 1 — PP01 Scope creep against fixed budget | Critical | Repeated when requirements evolve | Medium: original scope research; EX-B02 agreement tension | Work enters delivery without a baseline delta or authorized trade-off. Observe unpriced additions, CR lag and baseline disagreement. |
| 2 — PP03 Unclear criteria; defect versus enhancement dispute | Critical | Repeated at refinement, review and UAT | Medium: EX-B01 collaboration; original quality research | Different interpretations emerge after implementation. Observe rejected submissions, disputed classifications and rework. |
| 3 — PP04 Client decisions/sign-off delayed | High | Repeated, concentrated near gates | Medium: EX-B01; EX-B03/04 anecdotes | Decision owner lacks time, context or authority; dependent work waits. Measure elapsed decision time and blocked work, separating vendor preparation delay. |
| 4 — PP06 Low visibility and conflicting progress | High | Repeated in status cycles | Low–medium: practitioner/repository synthesis | Board completion is confused with acceptance/release; late work disappears from original Sprint history. Check disagreements between reports and evidence. |
| 5 — PP02 Change-request friction | High | Episodic, sometimes repeated | Medium: original change research; EX-B02 | Formal control is too costly for clarifications, or impact/authority is unclear. Observe decision turnaround and work started before resolution. |
| 6 — PP05 Milestone/invoice disputes | Critical | Episodic at payment gates | Low–medium: original contract research; EX-B05 authority case | Vendor considers delivery sufficient; client finance lacks agreed acceptance evidence. Observe disputed amounts and missing prerequisites, not just overdue invoices. |
| 7 — PP07 Estimate versus actual gap | High | Repeated during planning | Low–medium: original estimation research; owner capacity/timeline need | Assumptions, changing scope, blocked time and effort are conflated. Compare forecasts with actuals without scoring individuals. |
| 8 — PP08 Warranty/handover ambiguity | High | Concentrated at release and closure | Low: original lifecycle research; needs validation | No clear coverage, recipient or residual owner. Observe support bounce, missing access/runbooks and reopened obligations. |
| 9 — PP09 Duplicate entry and evidence drift | Medium | Repeated across tools | Low–medium: EX-B04 workaround; owner API idea | Status/version copied without provenance; reports and consumer integration records become stale. Observe reconciliation time and wrong-version decisions. |

High severity does not automatically imply an MVP financial subsystem. PP05 may initially be addressed by clear milestone obligations and external evidence links. Reorder this table after interviews with both sides and an artifact walkthrough; do not average a vendor's margin concern and a client's acceptance concern into a misleading score.

## Information asymmetry map

All rows below are **AI-inference**, grounded in the phase needs and source limitations above. These are gaps to test, not accusations that either side hides information.

| Gap / moment | What one side may know | What the other lacks | Consequence | Candidate shared view and privacy boundary |
|---|---|---|---|---|
| G01 Baseline setup | Vendor knows estimation exclusions and technical assumptions | Client may remember proposal outcomes rather than exclusions | PP01/03: incompatible expectations | Signed-input reference, assumptions and unresolved questions, with exact versions |
| G02 Backlog ordering | Client knows internal priorities and impending policy changes | Vendor sees only current tickets | PP01/07: implement low-value or soon-obsolete work | Ranked outcomes, decision rationale and effective date; no need to expose unrelated client strategy |
| G03 Mid-Sprint | Vendor knows blocked days, remaining effort and private cost | Client sees a green percentage or demo | PP06/07: late surprise | Shared delivery impact, actual blocked intervals and forecast uncertainty; internal margin/payroll remains private |
| G04 Review request | Vendor knows precisely which revision needs a decision | Client sees a long email or changed document | PP03/04: wrong-version approval or no response | Version-specific decision card, delta, criteria and named authority |
| G05 Client dependency | Client knows internal access/security queue status | Vendor sees silence | PP04/06: unrealistic recovery forecast | Named dependency owner, next check and stated uncertainty; internal tickets linked only when shareable |
| G06 UAT | Client testers know operational exceptions | Vendor knows implementation assumptions and test constraints | PP03/08: late gaps and classification disputes | Prepared scenarios, data/environment readiness and issue-to-criterion linkage |
| G07 Milestone payment | Vendor knows submitted evidence; client finance knows PO/payment prerequisites | Each sees only its own completion state | PP05: acceptance and collection conflated | Obligation/evidence checklist and external invoice status as of a date; restricted commercial fields |
| G08 API update | Backend knows new contract/build; frontend knows its consumed revision | Each assumes the other uses the latest version | PP09/03: broken integration and irrelevant test results | Revision → deployed build/environment → consumer evidence; notification is not proof of integration |
| G09 Closure | Vendor knows remaining coverage; operations knows support readiness | PM sees release complete | PP08: ownerless residual work | Handover recipient, warranty boundary and accountable open-obligation list |
| G10 Multi-organization planning | Employee knows outside commitments | PM lacks authorized availability | PP07: hidden over-allocation | Declared availability or consented aggregate constraint, never another employer's work details |

## Current tools and workarounds

These describe plausible workflows, not a comparative product benchmark. Failure conditions are **AI-inference** unless attributed. A good configuration and disciplined team may already solve the problem.

| Tool / practice | Why teams use it | Where the process can break | Product response or alternative |
|---|---|---|---|
| Jira / Azure DevOps work tracking | Work, boards, history and familiar team habits | Contract scope versions and client/commercial decisions may live elsewhere | Integrate or reference authoritative work; test whether added traceability beats configuring the existing tracker |
| Jira Service Management approvals | Existing approval steps and customer participation | A generic request approval alone may not link contract, version, test evidence and payment obligation | Counterexample: don't claim approvals are absent; compare the whole decision journey, EX-A03 |
| Excel / spreadsheets | Flexible estimates, capacity, budgets and UAT lists | Multiple copies, manual joins and unclear version/authority | Preserve useful imports/exports with provenance; avoid forcing finance data into the product |
| Email | Familiar, accessible client channel; can preserve approval replies, EX-B04 | Attachments diverge; “looks good” may not identify scope or authority | Link a focused decision request, capture attributable evidence and clarify its effect |
| Confluence / shared documents | Long-form requirements, decisions and design | Discussion can be mistaken for accepted scope; links may point to a changed page | Bind accepted revision; keep narrative authoring flexible |
| Chat / video calls | Fast clarification and relationship building | Decisions disappear into conversation; absent stakeholders miss context | Record concise decision outcome, owner and source; do not record every chat as a gate |
| API specifications / API clients | Interface documentation and manual integration checks | Current spec alone does not identify deployed revision or consumer compatibility | Connect revision, build, environment and consumer evidence; reuse standards |
| CI/CD and external performance tools | Reliable execution logs and specialist test capability | A green result may concern another build, workload or environment | Import/link provenance and thresholds; do not build another engine |
| PM's weekly slide/report | Tailored narrative and escalation | Manual synthesis can omit uncertainty or become stale immediately | Timestamped report snapshot with facts, forecasts, decisions and reviewable AI draft |

## Practical reporting example

**Illustrative proposal:** A Sprint ends Friday with 18 of the originally selected 26 points Done; an eight-point item is unfinished after an external dependency blocked it. The execution view retains Friday's result, the blocked interval and the later completion. The next Sprint counts the eight points only if selected and completed under its policy. Client acceptance is a separate event. A forecasted Tuesday finish is labelled forecast until observed.

This prevents two misleading reports: moving the first Sprint's end date to claim success, and counting the same eight points in both Sprints. It still does not tell us the vendor's profitability or contractual earned value. Use agreed deliverable weights/financial definitions for those purposes, not raw story points.

## Sources

- [Evidence register](01-evidence-review.md): EX-B01 collaboration study, EX-B02 client case, EX-B03 UAT anecdote, EX-B04 approval anecdote and EX-B05 PMI case, with limitations and direct links.
- [Atlassian approvals documentation](https://support.atlassian.com/jira-service-management-cloud/docs/what-are-approvals/) (EX-A03), accessed 2026-11-07; verifies an existing approval capability only.
- Original [scope/change](../05-scope-and-change-management.md), [governance](../06-risk-quality-and-governance.md), [offshore communication](../07-offshore-communication.md) and [pitfalls](../09-lessons-learned-and-pitfalls.md).
- [D1–D6](../../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions) and [canonical execution example](../../sdlc-workflow.md); product direction and proposed design, not empirical results.
