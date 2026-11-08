---
id: RESEARCH-DP-ANALYSIS-07
title: "07 — Opportunities and proposed priorities"
status: proposed
---

# 07 — Opportunities and proposed priorities

[Analysis index](README.md) · [Previous: Objects and rules](06-objects-lifecycles-and-rules.md) · [Next: Validation](08-assumptions-and-interviews.md)

This is **AI-inference** informed by the pain analysis and product-owner direction. MoSCoW here refers to the **first usable increment**, not the complete product. Must = without it the selected pilot journey fails; Should = valuable but can temporarily use a documented workaround; Could = useful after the core journey; Won't initially = explicit boundary or deferred work. Dates and effort are unestimated. The [roadmap](../../roadmap.md) is the canonical feature registry.

| Opportunity | How might we… | Candidate capability and feature trace | Pain/gap | First-increment priority and rationale |
|---|---|---|---|---|
| O01 | Make the current agreement unambiguous while allowing learning? | Exact-version baseline, assumptions, exclusions and linked backlog; FEAT-DP-006/007/008/017/025 | PP01/03, G01 | Must: no defensible fixed-price control without agreement context |
| O02 | Make a change decision proportionate to its impact? | Clarification/defect/change triage, scope-swap comparison, authorized CR disposition and explicit application; FEAT-DP-019/007 | PP01/02/05, G01/02 | Must: prevent work and agreement diverging silently |
| O03 | Help the right client person make the next decision quickly? | Named authority, criteria/version/delta, due date and focused client queue; FEAT-DP-007/015/026 | PP03/04, G04/05 | Must: pilot includes both sides; comments do not count as acceptance |
| O04 | Show what really happened without rewriting the Sprint plan? | Original-versus-actual timeline, blockers, carry-over and evidence freshness; FEAT-DP-009/010/011/027 | PP06/07, G03 | Must: explicit owner direction; manual actuals acceptable before connectors |
| O05 | Make project health explainable and reports cheaper to assemble? | Shared/internal health, weekly/Sprint report snapshots, decision queue and rule signals; FEAT-DP-001/010/027 | PP04/06/09, G03/05 | Must for manual/evidence-based report; AI drafting Should in subsequent increment |
| O06 | Keep workload useful without exposing other employers' work? | Tenant/project grants and declared available capacity; FEAT-DP-003/004/011/015 | PP07, G10 | Must for scoped identity/access; automated cross-employer allocation Won't initially |
| O07 | Help PMs compare feasible recovery options? | Capacity/dependency-aware scenarios and AI planning suggestions; FEAT-DP-013/018 | PP06/07, G03/05 | Could initially; full product scope, proposed V1 depth; no automatic timebox change |
| O08 | Make acceptance and payment conditions understandable to both sides? | Milestone obligations, acceptance evidence and external finance references; FEAT-DP-025/007/016 | PP03/05, G06/07 | Must for obligation/acceptance links; finance synchronization Could, native billing Won't initially |
| O09 | Find the relevant evidence without another reconciliation exercise? | Permission-aware relationship search and traceability; FEAT-DP-017 with owning capabilities | PP06/09, G04/08 | Should for broader search; direct trace links Must; search cannot reveal hidden item counts |
| O10 | Keep API producers, consumers and testers aligned on a usable revision? | Specification history, curl-assisted draft, availability notifications, consumer integration and test provenance/export; FEAT-DP-029/012/020 | PP03/09, G08 | Could in first increment; V1 candidate, validate developer/QA demand |
| O11 | Make release and support transfer a deliberate decision? | UAT/release evidence, warranty coverage, residual owners, handover and closure; FEAT-DP-012/016/030 | PP03/08, G06/09 | Could in first increment; required to complete eight-stage product coverage in V1 |
| O12 | Reduce manual evidence entry without duplicating specialist tools? | Reference authoritative sources first, pilot-selected adapters later; FEAT-DP-020 | PP06/09, G03/08 | Should for links/import provenance; Could for automated connector; proprietary load engine/source hosting Won't initially |

## Proposed sequence and decision gates

1. **Secure agreement-to-review:** access → signed-input context → baseline and criteria → fixed-timebox Sprint → actual evidence/blockers → client decision → controlled change. A user can complete this journey without an AI provider.
2. **Complete lifecycle:** detailed requirement collaboration, UAT/release, API evidence where justified, warranty/handover/closure. Until then, advertise only the delivered slice, not complete SDLC automation.
3. **Deepen assistance:** AI report/risk drafts, plan scenarios, selected integrations and forecasts. Evaluate against manual reports and deterministic rules before adopting more complex predictions.

This sequencing retains the prior roadmap's broad MVP/V1 progression while adding the owner's execution-view need to the first increment. Feature 029 owns API-specific collaboration, while 020 owns external adapters and 012 owns test evidence. Feature 030 fills warranty/closure ownership. No new feature is needed merely to render actual Sprint execution: 009 owns it, with 018 providing forecast/scenario depth.

## End-to-end pilot acceptance proposals

| Scenario | Observable success | Failure that should block pilot claims |
|---|---|---|
| Revised story after sign-off | Reviewer sees old accepted revision and new unapproved delta | Approval silently follows edited content |
| Client-requested scope swap | Team shows impact and authorized equal-price decision; baseline changes explicitly | Equal points alone treated as equal cost or permission |
| Blocked Sprint item | Original close/noncompletion retained; later completion traceable and counted once | Sprint extended or historical outcome erased |
| Client approval request | Authorized person can identify subject, criteria and effect with little navigation | Stakeholder feedback or silence creates acceptance |
| Shared health/search | Client sees authorized evidence and freshness | Private margin, hidden projects or cross-employer assignments leak |
| Milestone outcome | Acceptance, invoice eligibility and collected payment remain distinct | Sprint close marks invoice paid |
| AI recovery proposal | Sources and assumptions shown; stale inputs detected before application | AI changes baseline, sends promises or invents missing actuals |

These are design validation scenarios, not executed software tests or approved acceptance criteria. Detailed features still require the repository's normal UR → FR → US refinement.

## Sources

- [Pains/gaps](04-pains-gaps-and-workarounds.md), [personas](02-stakeholders-and-personas.md) and [evidence register](01-evidence-review.md): provenance and counterexamples.
- [Product-owner decisions D1–D6](../../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions), [product scope](../../product.md#8-product-scope-and-proposed-delivery-sequence) and [roadmap](../../roadmap.md): direction and registry, not customer validation.
