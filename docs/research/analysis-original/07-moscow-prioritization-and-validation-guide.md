# 07 — MoSCoW Feature Prioritization, Open Questions & User Validation Guide

[Analysis Index](README.md) · Previous: [06 — Core Objects, Lifecycles, and Fixed-Price Rules](06-core-objects-lifecycles-and-fixed-price-rules.md)

**Provenance Summary:** The MoSCoW prioritization table (`[PM/UX Inference]`) traces candidate product features directly to the ranked pain points (`PP-01`..`PP-12`) and information gaps (`IG-01`..`IG-08`) from [Chapter 03](03-pain-points-and-information-gaps.md), the `HMW` capabilities from [Chapter 04](04-workarounds-tools-and-opportunities.md), and the SDLC/object definitions in [Chapters 05](05-product-sdlc-definition-and-flows.md)–[06](06-core-objects-lifecycles-and-fixed-price-rules.md). The Open Questions & Assumptions Register and Interview Guides follow the discovery validation discipline in [`.agents/skills/dp-product-discovery/SKILL.md`](../../../.agents/skills/dp-product-discovery/SKILL.md).

> [!NOTE]
> **Governance & Roadmap Boundary:** Per [`AGENTS.md`](../../../AGENTS.md), [`docs/product.md`](../../product.md), and [`docs/roadmap.md`](../../roadmap.md), this MoSCoW list defines **what the product needs to digitalize the fixed-price Agile outsourcing SDLC based on discovery evidence**. It does not silently rewrite the repository's existing MVP vs. Next-Release slice allocation in [`docs/roadmap.md`](../../roadmap.md) or claim that these capabilities are already implemented in code.

---

## 1. Prioritized Feature List (MoSCoW) Traced to Pain Points & Information Gaps

### 1.1 MUST HAVE (Foundational Spine for Fixed-Price Agile Outsourcing)

Without these capabilities, teams immediately fall back to disconnected Excel spreadsheets, Word CRs, and disputed email threads (`[PM/UX Inference]`):

| Feature ID & Name | SDLC Phases Supported | Core Capability Summary | Traced Pain Points, Gaps & HMWs |
|---|---|---|---|
| **`F-01` Versioned Scope Baseline & Live Diff Ledger** | Contracting (`P2`), Inception (`P3`), Sprints (`P4`), UAT (`P5`) | Lock agreed URs, Features, Stories, and `AC` into numbered, immutable baselines (`B1.0`, `B1.1`). Visually badge every backlog item (`Baselined`, `Refined within Envelope`, `Candidate`, `Swapped Out`, `Pending CR`) and highlight live diffs when specs evolve. | `PP-01` (#1), `PP-02` (#2), `PP-10` (#10), `IG-03`, `HMW-01` |
| **`F-02` Dual-Track Backlog Item State Engine (`ScopeState` + `DeliveryState`)** | Inception (`P3`), Sprints (`P4`), UAT (`P5`), Release (`P6`) | Separate commercial authorization (`Candidate` vs. `Baselined`) from engineering execution (`Refining` → `Ready (DoR)` → `In Sprint` → `DoD Done` → `Sprint Reviewed` → `Client Accepted` → `Released`). Block unbaselined items from entering active sprints. | `PP-01` (#1), `PP-07` (#7), `PP-11` (#11), `IG-02`, `HMW-06` |
| **`F-03` Scoped Client Portal & Role-Based Authority Matrix** | All Phases (`P1–P7`) | Provide external Client PO, Sponsor, and UAT users with an intentionally scoped portal view (shared baselines, burn-up, decision queue, CR/Swap approvals, UAT sign-off) while strictly isolating vendor internal hourly costs, contingency, and private team notes. Enforce explicit per-role signature authority (`Requirement Approver`, `Milestone Acceptor`, `CR Signatory`). | `PP-05` (#5), `PP-07` (#7), `PP-08` (#8), `IG-01`, `IG-08`, `HMW-04`, `HMW-09` |
| **`F-04` Request Triage, Change Request (`CR`) & Guided Scope-Swap Workbench** | Sprints (`P4`), UAT (`P5`) | Triage every new request or UAT dispute as *Clarification, Defect, Enhancement, Scope Swap,* or *Disputed*. Provide a 5-check **Scope Swap Calculator** (Unstarted/Sunk Cost, Effort Delta, Tech/API Impact, QA Regression, SOW Authority) for $0 trade-offs alongside a structured **Paid CR** workflow (`CR-02`) that updates baseline and milestone revisions upon dual signature. | `PP-01` (#1), `PP-02` (#2), `PP-03` (#3), `IG-03`, `IG-05`, `HMW-02`, `HMW-03` |
| **`F-05` Milestone & UAT Acceptance Workspace with 5-Day Review Clock** | Inception (`P3`), Sprints (`P4`), UAT (`P5`), Handover (`P7`) | Package deliverable slices (`M1–M5`) with locked build IDs, requirement-to-test coverage, and QA evidence. Enforce a visible **5-business-day client review timer**, require baseline-linked evidence for any rejection, and support **Conditional Acceptance** with bound minor-defect fix dates and owners. | `PP-02` (#2), `PP-06` (#6), `PP-07` (#7), `IG-02`, `IG-08`, `HMW-06`, `HMW-07` |
| **`F-06` Client-Safe Release Burn-Up & Milestone Forecast View** | Sprints (`P4`), UAT (`P5`) | Plot `Original Baseline (120 pts)`, `Current Authorized Scope`, and `Cumulative DoD Done (54 pts)` with automated sprint-range forecasting (`3.67 sprints remaining vs. 3 planned`) so schedule risks and scope movement are visible to both sides early without manual Excel exports. | `PP-04` (#4), `PP-08` (#8), `IG-01`, `HMW-04` |
| **`F-07` SLA-Backed Offshore Decision Queue & Client Dependency Tracker** | Inception (`P3`), Sprints (`P4`), UAT (`P5`) | Structured asynchronous decision cards (`Options A/B, Recommendation, Blocked Stories/Milestones, Required-By Timestamp in both time zones, Deputy Auto-Escalation`) paired with gated **Client Dependency** items (`API Sandbox`, `Sample Data`, `UAT Testers`) that log schedule-impact evidence when late. | `PP-05` (#5), `PP-09` (#9), `IG-07`, `HMW-05` |

---

### 1.2 SHOULD HAVE (High-Leverage Commercial & Margin Protection)

These capabilities eliminate the Vendor PM's remaining spreadsheet workarounds and close the Presales-to-Warranty loop (`[PM/UX Inference]`):

| Feature ID & Name | SDLC Phases Supported | Core Capability Summary | Traced Pain Points, Gaps & HMWs |
|---|---|---|---|
| **`F-08` Vendor Internal Cost, Contingency & EVM Ledger** | Presales (`P1`), Sprints (`P4`), Closure (`P7`) | Internal vendor-only financial view tracking base cost budget (`$120k`), risk contingency reserve (`$18k` drawn against Risk IDs), incurred cost (`AC`), earned package value (`EV`), `CPI/SPI`, bottom-up vs. trend `EAC` (`$132k` vs. `$161k`), and projected gross contribution (`$48k`). | `PP-04` (#4), `PP-08` (#8), `IG-01`, `HMW-04` |
| **`F-09` Evidence-Backed Milestone Invoice Eligibility & Audit Packet** | Sprints (`P4`), UAT (`P5`), Handover (`P7`) | Lock milestone invoices (`M1–M5`) until their linked `Acceptance / Sign-Off` record is `Accepted` or `Conditionally Accepted`. Auto-compile a downloadable/shareable **Invoice Evidence Packet** (Signed Certificate + Baseline Diff + Test Coverage) and track the 15-day payment clock (`Eligible` → `Issued` → `Approved` → `Paid` / `Overdue`). | `PP-07` (#7), `IG-08`, `HMW-12` |
| **`F-10` Presales Range Estimator & Discovery-to-Baseline Converter** | Presales (`P1`), Contracting (`P2`), Inception (`P3`) | Capture presales feasibility spikes, role-effort cost models, team velocity ranges, priced assumptions, and exclusions; convert the signed proposal directly into **Baseline `B1.0`**, the **Kickoff Checklist**, and the **Risk Register** with zero handover loss. | `PP-04` (#4), `PP-10` (#10), `HMW-08` |
| **`F-11` Operational Handover Checklist & 30-Day Warranty Triage Desk** | Release (`P6`), Warranty/Handover (`P7`) | Gate final Milestone `M5` (`10%`) on verified operational rehearsals (backup restore, credential transfer, runbook verification). Start an automated **30-calendar-day Warranty Clock** at production cutover that separates covered baseline defects from paid post-launch enhancements. | `PP-12` (#12), `IG-04`, `HMW-10` |
| **`F-12` Rule-Based Delivery Risk & Scope-Creep Alerts** | Sprints (`P4`), UAT (`P5`) | Objective rule detectors (with optional AI explanation per [`docs/product.md`](../../product.md)) that alert the PM/PO when story splitting inflates baseline points, decision SLAs breach >48h, throughput drops below milestone burn rate, or UAT prerequisites are unbooked at `T-10 days`. | `PP-01` (#1), `PP-08` (#8), `PP-09` (#9), `IG-01`, `IG-05`, `HMW-11` |

---

### 1.3 COULD HAVE (Workflow Accelerators & Advanced Commercial Modes)

| Feature ID & Name | SDLC Phases Supported | Core Capability Summary | Traced Pain Points, Gaps & HMWs |
|---|---|---|---|
| **`F-13` Pre-Authorized Client Change Pool ("Change Token Wallet")** | Contracting (`P2`), Sprints (`P4`) | Support hybrid fixed-price contracts that include a pre-funded change buffer (e.g., `$15,000` or `20 points`) that the Client PO can draw down with 1-click approval before formal Paid CR signatures are required. | `PP-03` (#3), `IG-06`, `HMW-02` |
| **`F-14` Early-Exit ("Money for Nothing") Settlement Calculator** | Sprints (`P4`) | Optional contract module supporting Jeff Sutherland's early-termination clause (e.g., calculating the 20% settlement on remaining unbuilt baseline value at a Sprint boundary and generating the handover/closure package). | `PP-03` (#3), `IG-05` |
| **`F-15` One-Screen Weekly Status & Steering Deck Auto-Generator** | Sprints (`P4`), UAT (`P5`) | Compile the weekly/monthly status report ([`08-templates-and-checklists.md`](../08-templates-and-checklists.md)) directly from live sprint, burn-up, milestone, risk, and decision-queue data with separate Client and Internal Vendor views. | `PP-08` (#8), `IG-01`, `HMW-04` |
| **`F-16` Offshore Overlap Scheduler & Holiday-Aware SLA Calendar** | Inception (`P3`), Sprints (`P4`) | Automatically adjust daily overlap windows across DST transitions (e.g., `Europe/London` GMT/BST vs. `Asia/Ho_Chi_Minh` UTC+7) and pause 48h Decision Queue / 5-day UAT timers during declared client/vendor public holidays (e.g., Tet, UK Bank Holidays). | `PP-05` (#5), `HMW-05` |

---

### 1.4 WON'T HAVE (Explicit Non-Goals for Initial Product Definition)

Consistent with [`docs/product.md`](../../product.md) (`[Evidence: Existing Research]`):

* **`W-01` Full Accounting ERP / Tax / Payroll Processing:** The product tracks **Milestone Entitlement, Acceptance Evidence, and Invoice Lifecycle States (`Planned` → `Eligible` → `Issued` → `Paid`)**, but does not replace corporate general-ledger accounting, tax compliance, or bank wire execution.
* **`W-02` Automated Individual Developer Ranking by Points/Hours:** Story points and logged hours are used strictly for project-level forecasting and internal cost-baseline management, never as automated employee performance scores (`[Evidence: Existing Research]` — [`docs/product.md`](../../product.md)).
* **`W-03` Autonomous AI Approval of Baselines or Change Requests:** AI may assist with drafting impact analyses, suggesting scope swaps, or summarizing blockers, but **human decision owners retain 100% authority** over baselines, CRs, UAT sign-offs, and releases (`[Evidence: Existing Research]` — [`docs/product.md`](../../product.md)).

---

## 2. Open Questions & Riskiest Assumptions to Validate

Before locking detailed feature specifications (`UR`/`FR`/`US`), the following assumptions (`[User Assumption]` and `[PM/UX Inference]`) carry the highest product and adoption risk and must be validated with real Vendor PMs/POs and Client POs/Sponsors:

| ID | Assumption / Open Question | Category | Risk if Wrong (Impact × Likelihood) | Validation Method & Target Metric |
|---|---|---|---|---|
| **`ASM-01`** | **Client Portal Adoption:** External Client POs and Sponsors will actively log into the vendor's platform to answer Decision Queue items, approve Scope Swaps, and sign off UAT—rather than replying via email/Slack and forcing the Vendor PM to transcribe their answers. | Adoption / UX | **High / High:** If clients refuse to log in, dual-party workflows become single-player administrative overhead for the Vendor PM. | **Interview + Prototype Test:** Test whether magic-link / email-actionable approval cards (with 1-click portal view without complex account setup) are required for client adoption. |
| **`ASM-02`** | **Transparency of the Burn-Up & Forecast Line:** Vendor PMs will be willing to share a live, unedited Release Burn-Up and completion forecast with clients in real time, rather than curating/smoothing the numbers in a weekly snapshot before the client sees them. | Commercial / Trust | **High / High:** Many agency PMs fear that a live automated forecast showing a 1-sprint dip in Sprint 2 will trigger premature client panic before the team recovers. | **Vendor PM Interviews:** Compare preference between *(A) Real-time shared burn-up* vs. *(B) PM-reviewed "Publish Sprint Snapshot to Client Portal" gate*. |
| **`ASM-03`** | **Strictness of Unbaselined Sprint Locks:** Blocking a team from pulling a `Candidate` (unbaselined) story into an active Sprint will prevent scope creep without paralyzing urgent, good-faith fixes during delivery. | Workflow / Agility | **Medium / High:** Overly rigid gate locks cause teams to bypass the tool or stuff extra work inside existing baselined tickets. | **Vendor PO/BA & Scrum Master Interviews:** Validate whether a *"Provisional Pull with 48h Retroactive Swap/CR Reconciliation"* escape hatch is needed. |
| **`ASM-04`** | **Client Willingness to Classify Rejections:** Client UAT leads will accept a system rule that requires linking a UAT `Rejection` to a specific baselined Acceptance Criterion (`AC1.0`) rather than rejecting a milestone for general dissatisfaction. | Commercial / Legal | **High / Medium:** Clients may feel the software is "biased toward the vendor" if they cannot flag usability/holistic workflow issues during UAT. | **Client PO / UAT Interviews:** Test adding a *"Holistic / UX Feedback -> Convert to Fast-Track Scope Swap"* path alongside strict contractual defect logging. |
| **`ASM-05`** | **Jira Coexistence vs. Replacement:** Agencies will adopt DeliveryPilot as their primary backlog/sprint execution tool (` replacing Jira`), rather than demanding a 2-way Jira sync where engineers stay in Jira and only PMs/Clients use DeliveryPilot. | Market / Technical | **High / High:** Engineering teams are deeply entrenched in Jira/GitHub workflows (`[Evidence: External Source]` — [`r/projectmanagement`](https://www.reddit.com/r/projectmanagement/)). | **Discovery Interviews:** Ask agencies whether they want an *all-in-one replacement* (explicitly chosen in [`docs/product.md`](../../product.md) for MVP) or if specific import/webhook bridges are dealbreakers. |
| **`ASM-06`** | **Granularity of Internal Cost Tracking:** How do target agencies actually track `AC` (Actual Cost) per sprint—via granular per-story timesheets, blended weekly role allocations, or sprint-level team burn rates? | Domain / Data Model | **Medium / Medium:** Requiring per-story hour logging from developers creates high friction and inaccurate timesheet data. | **Vendor PM Interviews:** Validate whether *role-based weekly allocation × blended cost rate* is preferred over *per-ticket hour logging*. |

---

## 3. User Discovery Interview Guide (Vendor PMs/POs & Client POs)

Use this **35–45 minute semi-structured interview script** to validate `ASM-01` through `ASM-06` without pitching the solution upfront (following Mom Test / UX discovery best practices: ask about **actual past behavior on their last fixed-price project**, not hypothetical future preferences).

### 3.1 Track A: Interview Guide for Vendor PMs & Vendor POs/BAs

**Target Participant:** Delivery Manager, Senior PM, or Proxy PO/BA at a software outsourcing agency (10–200 staff) who has delivered at least two fixed-price or capped-budget Agile Scrum projects in the past 12 months.

1. **Warm-Up & Context (5 min)**
   * *"Walk me through the most recent fixed-price (or capped-budget) Agile project you delivered for an external client. What was the rough team size, duration, and milestone structure?"*
   * *"How was the Product Owner role split between your side and the client's side?"*
2. **Presales Handover & Baseline Reality (7 min) — Validates `ASM-03`, `PP-01`, `PP-10`**
   * *"When that project kicked off, where did the agreed scope baseline actually live? If I looked at your Jira backlog in Sprint 3, how could I tell which stories were in the original SOW price versus added later?"*
   * *"Tell me about the last time scope crept in during backlog refinement or a Sprint Review without a formal Change Request. How did it happen, and when did you realize it was hurting your budget?"*
3. **The Friday Reporting & Burn Workaround (8 min) — Validates `ASM-02`, `ASM-06`, `PP-04`, `PP-08`**
   * *"Show me (or describe) the exact spreadsheets and tools you open at the end of a sprint to figure out: (a) whether you'll hit the deadline, and (b) whether your internal cost budget/margin is safe."*
   * *"How do you track internal cost burn (`AC`)—do devs log hours on stories, or do you track team burn by week? How long does it take you to reconcile that with delivery progress?"*
   * **Probe on `ASM-02`:** *"If a dashboard calculated your release burn-up and completion forecast automatically at the end of every sprint, would you want the client to see it live in real time, or would you insist on reviewing and publishing a snapshot first? Why?"*
4. **Change Requests, Scope Swaps & Client Delays (8 min) — Validates `PP-03`, `PP-05`, `PP-09`**
   * *"Tell me about the last time the client asked for a new feature mid-project. Did you do a formal paid CR, a scope swap, or absorb it? What made that conversation easy or painful?"*
   * *"What happened the last time a client PO took 4+ days to answer a question or client IT delivered an API late? How did you track that delay, and were you able to adjust the milestone date or charge for the blocked time?"*
5. **UAT, Sign-Off & Invoicing Friction (7 min) — Validates `ASM-01`, `ASM-04`, `PP-06`, `PP-07`**
   * *"Walk me through the last UAT or milestone sign-off that got delayed or disputed. How many items did the client log as 'bugs' that your team considered 'new scope'? How did you prove what was originally agreed?"*
   * *"What had to happen before your Finance team could actually send the milestone invoice and get it paid?"*
6. **Concept Reaction (5 min) — Validates `ASM-01`, `ASM-05`**
   * *"If your workspace had a built-in Client Portal where the Client PO could approve a 1-click Scope Swap or sign off a Milestone with a 5-day countdown timer, what would stop your current clients from actually clicking that button?"*

---

### 3.2 Track B: Interview Guide for Client POs & Client Sponsors

**Target Participant:** Product Owner, Digital Transformation Lead, IT Director, or Project Sponsor who has hired an external software agency under a fixed-price or milestone-based contract in the past 18 months.

1. **Warm-Up & Engagement Setup (5 min)**
   * *"Tell me about the last software project you outsourced to an external agency on a fixed-price or milestone basis. What was your role day-to-day, and how much time per week did you realistically have for the project?"*
2. **Visibility & The "Black Box" Problem (8 min) — Validates `IG-01`, `IG-02`, `PP-08`**
   * *"During the middle sprints of that project, how did you know whether the agency was genuinely on track to hit your launch date? Did you ever get a surprise where status reports looked green, and then suddenly a deadline slipped?"*
   * *"Did the vendor give you access to their Jira board? If yes, was it helpful or overwhelming? What information were you actually looking for that was hard to find?"*
3. **Evolving Needs, Swaps & Change Request Friction (10 min) — Validates `PP-03`, `IG-05`, `IG-06`**
   * *"Once you started seeing working demos in Sprint Reviews, you probably realized some features needed to work differently than what was written in the original SOW. Tell me about a specific change you wanted—how did the vendor react?"*
   * *"Did you ever try to swap out a lower-priority feature from the original scope for a new one of similar size? How did the vendor evaluate whether it was a 'fair swap'?"*
   * *"Did you have an internal contingency budget set aside for changes, or was the SOW price a strict ceiling where any extra invoice required painful executive approval?"*
4. **Decision Latency & UAT Sign-Off (8 min) — Validates `ASM-01`, `ASM-04`, `PP-05`, `PP-06`, `PP-07`**
   * *"When the offshore/vendor team needed quick product decisions from you, how did they send them? What happened when you were in back-to-back internal meetings for a couple of days?"*
   * *"Walk me through the UAT and milestone sign-off process. When the vendor asked you to sign off a milestone to release a payment, what gave you confidence—or hesitation—before signing?"*
   * **Probe on `ASM-04`:** *"Did you ever have a disagreement in UAT where you felt something was broken, and the vendor claimed 'it matches the spec, so that's a paid Change Request'? How was that resolved?"*
5. **Portal & Workflow Adoption (4 min) — Validates `ASM-01`**
   * *"When the vendor needed a formal sign-off on requirements, a scope swap, or UAT, how would you prefer to review and approve it? What would make you love—or hate—logging into a shared client portal with the vendor?"*

---

## Sources for Chapter 07

1. Repository Context & Governance: [`docs/product.md`](../../product.md), [`docs/roadmap.md`](../../roadmap.md), [`docs/requirements.md`](../../requirements.md), [`docs/deliverypilot-workflow-discovery.md`](../../deliverypilot-workflow-discovery.md), and [`docs/authoring.md`](../../authoring.md).
2. Discovery Skill Method: [`.agents/skills/dp-product-discovery/SKILL.md`](../../../.agents/skills/dp-product-discovery/SKILL.md) and [`references/evidence-and-routing.md`](../../../.agents/skills/dp-product-discovery/references/evidence-and-routing.md).
3. Preceding Analysis Chapters: [Chapter 01](01-stakeholders-and-personas.md), [Chapter 02](02-user-needs-by-sdlc-phase.md), [Chapter 03](03-pain-points-and-information-gaps.md), [Chapter 04](04-workarounds-tools-and-opportunities.md), [Chapter 05](05-product-sdlc-definition-and-flows.md), and [Chapter 06](06-core-objects-lifecycles-and-fixed-price-rules.md).
