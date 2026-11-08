---
id: RESEARCH-DP-ANALYSIS-08
title: "08 — Assumptions, open questions and interview guide"
status: proposed
---

# 08 — Assumptions, open questions and interview guide

[Analysis index](README.md) · [Previous: Opportunities](07-opportunities-and-priorities.md)

Everything in this validation plan is **AI-inference**. No interviews or prototype tests have been performed. The owner has settled the post-contract boundary, eight lifecycle stages, preserved Sprint timebox and broad AI/evidence direction; research should validate the operating details and customer value, not repeatedly ask whether those decisions exist.

## Assumption register

| ID | Assumption or unresolved decision | Risk if wrong | Validation and decision owner |
|---|---|---|---|
| A01 | Vendors will buy a shared agreement-to-delivery product | High: no viable buyer or purchasing path | Compare recent purchases, budget and alternatives with delivery heads; product lead |
| A02 | Clients will use a focused decision workspace | High: approval friction increases | Observe client completing a decision via prototype versus current email/link flow; product/UX lead |
| A03 | Version-bound baseline and CR coordination is a top pain | High: priority wrong | Review two recent disputed changes per side, including a smooth project; product lead |
| A04 | Optional dual-side story review needs configurable authority and exceptions | High: every story gets blocked or contractual authority bypassed | Inspect actual approval/delegation policy; client PO and vendor PM/commercial owner |
| A05 | Vendor-private cost can coexist with sufficient client progress transparency | High: trust/privacy conflict | Field-by-field sharing exercise on a redacted report; sponsor and vendor commercial owner |
| A06 | Teams can capture actual execution without onerous timesheets | Medium: timeline unreliable or rejected | Observe how starts, blocked periods and completion are recorded; PM/team lead |
| A07 | Carry-over, split and reopened work can use one coherent credit policy | Medium: misleading progress | Walk through real examples with team; preserve history and prevent duplicate credit; PO/SM |
| A08 | First API collaboration value exceeds integration/setup cost | Medium: unnecessary breadth | Follow one revision through BE, FE and QA tools; engineering/QA lead |
| A09 | External build/environment/test evidence is sufficiently accessible | High: claims cannot be substantiated | Inspect sample payloads and permissions without collecting secrets; integration owner |
| A10 | Warranty and final closure require separate evidence | Medium: workflow too rigid or too weak | Review contract coverage, handover and remaining obligations; PM/client operations |
| A11 | AI drafts save time beyond deterministic reminders and templates | Medium: cost/noise exceeds benefit | Blindly compare manual/template/AI reports for factual accuracy, edits and time; product/PM |
| A12 | Multi-employer participation is common enough for richer planning | Medium: privacy cost without demand | Validate membership usage; start with scoped identity and declared availability; product/privacy owner |
| A13 | Milestone/payment references suffice initially | Medium: buyers demand accounting or do not need monetary fields | Observe a finance evidence handoff; PM and client/vendor finance |
| A14 | Eight stages communicate status without implying exclusive waterfall gates | Medium: misleading mental model | Ask participants to represent two releases in different states; product/UX lead |

For each assumption record evidence for and against, affected feature IDs, decision owner, date and next action. Confidence must not be promoted to approval. Stop an experiment that would expose another organization's work; use consented/redacted artifacts.

## Recruitment and method

Proposed first round: 4–6 vendor PM/POs and 4–6 client POs across several completed or active fixed-price projects, ideally including matched pairs with separate consent. Include smooth and troubled projects, dedicated and part-time client POs, offshore time-zone differences, and different tool maturity. This is a qualitative discovery sample, not a statistical prevalence study. Seek additional finance, client UAT and engineering participants where a workflow question exceeds the PO's knowledge.

Use a 45-minute interview: 5 minutes context, 20 minutes recent-project walkthrough, 10 minutes a specific difficult decision, 5 minutes counterexamples/alternatives, 5 minutes optional concept feedback. Ask about actual behavior before showing DeliveryPilot. Request redacted artifacts only with permission; do not collect contract secrets or employee evaluation data unnecessarily.

## Vendor PM / PO interview guide

1. Tell me about your most recent signed fixed-price project. What exactly became the starting agreement for delivery?
2. Walk me through the last requirement that changed after agreement. Who noticed it, who assessed it, who could decide, and where did the decision end up?
3. Show how you knew a story was ready and Done. When did client acceptance happen, and what if the client disagreed?
4. Take a Sprint that ended with unfinished work. What stayed in the original report, what moved, and how did you explain later completion?
5. How did you calculate available capacity and forecast milestone impact? What inputs were unknown or stale?
6. Reconstruct the last weekly report. Which tools and people supplied each fact, and how long did reconciliation take?
7. Describe the last delayed approval or invoice-related dispute. What evidence was missing, and who had authority to resolve it?
8. What happened after go-live? Who owned warranty triage, handover and final closure?
9. Where does your existing toolchain work well? What would make an additional platform worse?
10. If an AI assistant suggested a recovery plan, what would you verify before using it? What must it never decide?

Follow-ups: “What happened next?”, “Can you show the version used?”, “Was that typical or an exception?”, “What did the other side believe?”, “What did it cost in time or rework?” Avoid leading with “Would version control solve this?”

## Client PO interview guide

1. How did you become responsible for this project? Which decisions could you make, and which needed sponsor, security, finance or operations?
2. What did you understand was included when delivery started? Where would you look now to settle a disagreement?
3. Walk through the last request for your approval. What were you asked to accept, what did you inspect, and how did you respond?
4. Describe a time a requirement changed or an expected behavior was missing. How was defect versus extra scope decided?
5. Which progress information helped you act? When were you surprised, and what earlier evidence would have mattered?
6. How did you prepare people, data and time for UAT? What happened to failed or disputed scenarios?
7. Did accepting delivery affect payment? Who checked the evidence, and what remained unresolved?
8. What did your operations team need at release/handover? Who handled defects after go-live?
9. What makes a vendor portal useful or burdensome? Compare a recent effective email or meeting with a frustrating one.
10. What should an AI-generated report disclose about evidence, uncertainty and who reviewed it?

## Prototype tasks and evidence to collect

| Task | Observe | Falsifier / design response |
|---|---|---|
| Decide on a revised story | Can person identify changed content, version, authority and decision effect? | Wrong-version acceptance → simplify comparison and decision language |
| Explain a late Sprint item | Can person distinguish planned end, actual completion and forecast? | Thinks Sprint was extended → redesign visual labels and original snapshot |
| Choose a scope trade-off | Can both parties identify excluded work, price effect and decision owner? | Equal points interpreted as commercial equivalence → expose assumptions/impact |
| Find an employee's work | Can user find authorized roles/items without inferring hidden organizations? | Hidden counts or snippets leak → permission model blocks release |
| Trace API performance evidence | Can QA identify revision/build/environment and relevant test conditions? | Result accepted for wrong deployment → require provenance/context |
| Read a health/report draft | Can reviewer separate facts, inference and missing data? | AI fluency hides uncertainty → constrain draft and require evidence checks |
| Close one release while another develops | Can participant see different UAT/warranty/closure obligations? | One project status suppresses obligations → revise lifecycle summary |

Compare task accuracy, decision time, clarification questions, number of tools opened and confidence against the current method. Agree success thresholds before the pilot; none are claimed here. Synthesize by participant/role/project and preserve disagreements. A successful concept test is still not legal approval, production readiness or proof of ROI.

## Sources

- [GOV.UK: Using in-depth interviews](https://www.gov.uk/service-manual/user-research/using-in-depth-interviews), accessed 2026-11-07; method reference, not validation of this sample.
- [Evidence review](01-evidence-review.md), [personas](02-stakeholders-and-personas.md) and [pains/gaps](04-pains-gaps-and-workarounds.md) provide the questions' provenance.
- [D1–D6](../../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions) distinguish settled owner direction from assumptions requiring customer evidence.
