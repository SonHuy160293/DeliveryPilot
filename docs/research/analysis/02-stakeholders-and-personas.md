---
id: RESEARCH-DP-ANALYSIS-02
title: "02 — Stakeholders and personas"
status: proposed
---

# 02 — Stakeholders and personas

[Analysis index](README.md) · [Evidence review](01-evidence-review.md) · [Next: Phase needs and journeys](03-phase-needs-and-journeys.md)

**User-provided:** The product serves both vendor and client, and the six persona groups below are requested analysis scope. **Repository-verified:** The current [product blueprint](../../product.md) describes a vendor-owned workspace, restricted client participation, human decision authority, and delivery risk as the primary differentiation. Neither statement establishes customer validation or implemented functionality.

**AI-inference:** These are proposed behavioral personas, not profiles of interviewed customers. Responsibilities, measures, interaction preferences, and tensions below are hypotheses synthesized from the existing research and external accounts. They need testing with real engagements. One person may fill several roles; several people may share a persona's work. Effective permissions and decision assignments remain separate from job titles, consistent with BR-003–BR-006 in [cross-feature requirements](../../requirements.md).

> **Scope reconciliation — 2026-11-07:** D1–D6 establish post-contract delivery across eight governance stages. Presales/contracting remain upstream context; native invoicing remains external. See the [current lifecycle](../../sdlc-workflow.md) and [analysis index](README.md). Detailed policies here remain proposals.

## Evidence shaping the personas

| Evidence | What was observed or reported | Interpretation used here |
|---|---|---|
| EX-B01 — Hoda, Noble, Marshall | A qualitative study of 30 practitioners across 16 organizations in New Zealand and India identified insufficient customer collaboration, including unavailable or ineffective representatives, and consequences for clarification, prioritization, and feedback. | Test whether decision capacity and delegation matter as much as document access. The older qualitative sample cannot establish current prevalence. |
| EX-B02 — West, Scrum.org | A client describes selecting a web agency willing to work with a dedicated client PO and shared staging, using commitments to a few sprints at a time. | Include an actively collaborating client PO, while testing variants with less availability. This is a rolling capacity arrangement, not proof about fixed-total-price outcomes. |
| EX-B03 — Client-side UAT forum account | A business system administrator reports learning late about their project involvement while training and UAT were scheduled together near go-live. | Test early nomination, preparation, and protected time for client testers. The account is unverified and contract type is unknown. |
| EX-B04 — Approval-practice forum discussion | A vendor-facing PM describes saved email approvals; replies describe live reviews, Jira records, and other tools. | Test low-friction participation and durable evidence before requiring clients to adopt another full PM workspace. Individual replies are anecdotes. |
| EX-B05 — Belling, PMI case | A client PM describes a sponsor's reluctance to enforce contractual scope because of competing interests. | Record decision ownership and unresolved escalations; a workflow cannot guarantee that an authorized person will use their authority. |

The following profiles are derived proposals. Source support establishes relevant problems and possible behavior, not every detail of each persona.

## P01 — Vendor project manager

| Dimension | Proposed profile |
|---|---|
| Goal | Deliver the agreed outcome while protecting the vendor's commitments, relationship, cash flow, and cost exposure. |
| Responsibilities | Establish delivery assumptions; coordinate dependencies and staffing; maintain forecasts and risks; reconcile scope changes; assemble acceptance evidence; escalate decisions; coordinate closure. |
| Decisions | Choose mitigation and resequencing within authority; recommend commercial changes. Commit price/date amendments only when explicitly authorized. |
| Must know | Current contractual baseline; actual delivery evidence; remaining work and uncertainty; client decision deadlines; milestone acceptance; internal cost forecast; unresolved obligations. |
| Success means | Fewer surprises and unauthorized additions; timely decisions; accepted deliverables with traceable evidence; viable delivery economics; a supportable handover. |
| Candidate measures | Time spent preparing status reports; decision waiting time; changes discovered after implementation; forecast error at comparable checkpoints; disputed milestone age. No target values established. |
| Likely workaround | Reconcile board exports, cost spreadsheets, meeting notes, and approval emails into a client report. |
| Tension to investigate | Pressure to present confidence may conflict with disclosing uncertainty early. Internal staffing or margin data may be useful to management but unsuitable for unrestricted client access. |
| Product interaction hypothesis | A daily exceptions view showing the decision needed, affected commitment, owner, deadline, and evidence, with a preview of exactly what the client will see. |

Relevant pains: PP01, PP02, PP04, PP05, PP06, PP07, PP08. Validate whether the vendor PM is also the commercial decision-maker or only the coordinator; this affects every approval path.

## P02 — Vendor PO/BA or client-PO proxy

| Dimension | Proposed profile |
|---|---|
| Goal | Turn business intent into understandable, testable work while keeping unresolved product choices visible. |
| Responsibilities | Facilitate discovery; refine requirements and backlog items; write examples; identify contradictory requests; maintain links between scope, criteria, decisions, and work; prepare impact analysis. |
| Decisions | Clarify within recorded delegation; propose ordering and scope trade-offs. Escalate new business rules, changed obligations, and commercial decisions. |
| Must know | Client priorities and rationale; who can answer each question; accepted requirement versions; criteria; data/integration constraints; pending changes and their implementation status. |
| Success means | Developers can start the right work and demonstrate it; client feedback addresses actual decisions; fewer avoidable interpretation loops and disputed “small changes.” |
| Candidate measures | Clarification cycle time; items returned for missing criteria; rework linked to misunderstood requirements; decisions without a named owner. |
| Likely workaround | Maintain a Confluence page or spreadsheet beside Jira and paste email decisions into both. |
| Tension to investigate | The team expects immediate answers while the client has not delegated authority or time. A vendor-employed PO may also feel pressure to favor delivery convenience over client value. |
| Product interaction hypothesis | A comparison of requested versus agreed behavior, a question queue, and an explicit “awaiting client decision” state that does not look like approval. |

Relevant pains: PP01, PP02, PP03, PP04. The [existing Scrum research](../04-scrum-operating-model.md) distinguishes a supporting proxy from the single accountable PO. Confirm which person holds that accountability for each engagement rather than creating two competing POs by default.

## P03 — Delivery team

| Dimension | Proposed profile |
|---|---|
| Goal | Produce useful, verified software at a sustainable pace with clear dependencies and quality expectations. |
| Responsibilities | Assess feasibility; estimate work; plan implementation; develop, integrate, test, and document; surface blockers and risks; provide release and operational evidence. |
| Decisions | Developers organize their work and assess feasible scope; technical choices follow the project's constraints. They do not grant client acceptance or authorize extra fees merely by implementing a request. |
| Must know | Sprint Goal; current selected work; baseline/change authority; acceptance examples; available environments; decision owners; build/test evidence; release constraints. |
| Success means | Small usable increments meet the agreed quality bar; fewer blocked days and late discoveries; operations can support the result. |
| Candidate measures | Blocked-work age; escaped defects by impact; carry-over reasons; integration failure recovery time; operational readiness evidence. Avoid ranking individuals by points or hours. |
| Likely workaround | Work in the existing issue tracker, repository, CI, test tool, and chat; leave the PM to translate activity into contractual progress. |
| Tension to investigate | Duplicate status entry and pressure to increase apparent velocity may undermine accurate reporting. A late client clarification may create rework even without a nominal scope increase. |
| Product interaction hypothesis | Show the agreed criteria and decision provenance beside work; capture evidence with minimal duplicate entry; distinguish engineering completion from client acceptance. |

Within P03, a technical lead needs dependency/feasibility context; QA needs testability and evidence; DevOps needs environment, release, rollback, and ownership information. These skill differences should guide views and assignments rather than force identical permissions.

Relevant pains: PP02, PP03, PP04, PP06, PP07, PP08.

## P04 — Client Product Owner

| Dimension | Proposed profile |
|---|---|
| Goal | Obtain the highest-value usable outcome within the authorized investment and business constraints. |
| Responsibilities | Reconcile stakeholder needs; order priorities; clarify business rules; supply examples; review working increments; arrange users; make or route acceptance and change decisions. |
| Decisions | Product choices within authority; accept specified deliverables only if assigned that power. Budget increases and altered contractual obligations may require sponsor/procurement approval. |
| Must know | What was agreed; what works now; what is awaiting a decision; available trade-offs; implications for business outcomes, dates, and commercial commitments. |
| Success means | The delivered product fits real work; stakeholders understand trade-offs; decisions arrive before they become expensive; no surprise invoice or unusable launch. |
| Candidate measures | Decision turnaround; business scenarios validated early; stakeholder disagreement reopened after approval; outcome adoption after release. |
| Likely workaround | Review a demo and email summary, consult internal colleagues, then respond through chat/email while the vendor updates the tracker. |
| Tension to investigate | The client PO may be accountable for decisions without control over finance, legal, data owners, or colleagues' calendars. A formal sign-off can feel like losing the right to report a defect. |
| Product interaction hypothesis | A concise decision inbox: requested action, exact version, plain-language change summary, affected obligations, evidence, and clear options to question, reject, or approve. |

Relevant pains: PP01, PP02, PP03, PP04, PP05, PP06. EX-B02 supports testing an active client-PO partnership; EX-B01 warns against assuming that availability already exists.

## P05 — Client sponsor and finance

These are two related stakeholders in one requested persona group. **Do not implement “Sponsor/Finance” as a single permission bundle.** The following split is a proposed authority model to validate.

| Dimension | Sponsor | Finance/procurement or accounts payable |
|---|---|---|
| Goal | Realize the business case and control organizational exposure. | Ensure spending is authorized, accurately evidenced, and processed under purchasing controls. |
| Responsibilities | Fund the initiative; resolve competing priorities; appoint accountable client representatives; decide escalated trade-offs. | Check purchase-order/contract references, milestone evidence, invoice data, disputes, and payment status. |
| Decisions | Funding, major scope/date changes, continuation or termination within authority. | Financial checks and payment actions within authority; no implied permission to accept software functionality. |
| Must know | Business consequence of a decision, options, remaining exposure, and last useful decision time. | Which obligation earned payment, who accepted which version, approved change amounts, due terms, disputed balances. |
| Success means | Benefits and risks remain acceptable; no unmanaged strategic surprise. | Correct payment with an explainable audit trail and fewer invoice exceptions. |
| Candidate measures | Escalation age; decisions revisited without new evidence; benefits reviewed at agreed points. | Invoice exception rate and resolution time; mismatch between milestone entitlement and billed amount. |
| Workaround | Steering deck and internal email decisions. | Purchase-order/ERP records, invoice PDFs, and approval attachments. |
| Product interaction hypothesis | An occasional action-focused view of material decisions. | An evidence package/reference that can support existing financial processing. |

Relevant pains: PP02, PP04, PP05, PP06, PP07. EX-B05 is a counterexample to assuming that more documentation alone resolves a sponsor conflict. The current blueprint excludes invoicing from MVP; this persona's needs inform later capability or integration decisions in [opportunities and priorities](07-opportunities-and-priorities.md).

## P06 — Client QA/UAT participants

| Dimension | Proposed profile |
|---|---|
| Goal | Establish that the candidate supports agreed business scenarios and make residual operational risks visible before acceptance or release. |
| Responsibilities | Help define scenarios; prepare representative data; reserve tester time; run and document tests; report reproducible findings; retest fixes; prepare an acceptance recommendation. |
| Decisions | Record test outcomes and business impact; recommend acceptance or rejection. Formal acceptance and release require separately assigned authority. |
| Must know | Candidate build/environment; included scope; test instructions and criteria; known defects; test-data responsibility; retest status; required decision date. |
| Success means | Important business paths are exercised; findings are reproducible; unresolved risks and conditions have owners; users are prepared for the release. |
| Candidate measures | Blocked tests caused by missing data/access; scenario coverage; retest turnaround; material issues first found after acceptance. |
| Likely workaround | Spreadsheet test scripts and defect logs, screenshots in email, and a meeting to reconcile results with the vendor. |
| Tension to investigate | Testing is often additional work alongside an operational job. Training, exploration, acceptance testing, and production readiness can be confused or scheduled together. |
| Product interaction hypothesis | A focused assigned-test and findings view with access instructions, version context, simple evidence capture, and a route to ask questions. |

Relevant pains: PP03, PP04, PP06, PP08. EX-B03 motivates validating preparation and role assignment early; it does not establish that a particular UAT duration fits every project.

## Buyer, user, and access hypotheses

The repository's initial market hypothesis is a software vendor with roughly 10–200 staff. A delivery head or executive may buy the product while a vendor PM uses it daily and a client sponsor uses it occasionally. **AI-inference:** Adoption may fail if client participation costs more attention than the decisions it simplifies. Test a focused shared workspace against links/notifications feeding an existing tool; avoid assuming the client will accept the vendor's preferred platform.

Proposed visibility begins with the information needed to act. Client-visible material may include agreed scope, submitted evidence, authorized commercial changes, and explicitly shared risk/health. Internal labor cost, margin, unshared estimates, personnel notes, and other engagements need separate controls. “Client cannot see real burn” can mean missing commitment risk; it does not automatically justify exposing supplier economics. Validate the disclosure agreement and offer evidence-backed summaries where appropriate.

Additional stakeholders—vendor sales/commercial owner, Scrum Master, client IT/security/operations, legal/procurement, and third-party integration owners—need assignment and involvement when their decisions constrain delivery. The six core personas do not grant them authority by proxy. Compare these roles with the phase needs and the [proposed SDLC gates](05-product-sdlc-flow.md).

## Sources

All external sources accessed 2026-11-07. Source IDs are stable within this analysis. Forum timestamps are relative in the retrieved page; no exact publication date is asserted.

- **EX-B01:** Rashina Hoda, James Noble, Stuart Marshall, [The impact of inadequate customer collaboration on self-organizing Agile teams](https://rashina.com/wp-content/uploads/2011/06/hoda-ist2011-impactcustinvol.pdf), *Information and Software Technology* 53 (2011), 521–534; online 16 November 2010. Primary qualitative study; sample and age limit generalization.
- **EX-B02:** Dave West, [Working with a web development company using Scrum](https://www.scrum.org/resources/blog/working-web-development-company-using-scrum), 19 February 2016. Firsthand client account of a proposed partnership; rolling sprint capacity and no claimed causal outcome evaluation.
- **EX-B03:** r/projectmanagement, [When in the project lifecycle would you aim to do UAT?](https://www.reddit.com/r/projectmanagement/comments/16zg8o5/when_in_the_project_lifecycle_would_you_aim_to_do/), retrieved page shows “3y ago.” Anonymous client-side account and replies; identities, contract, and outcomes not independently verified.
- **EX-B04:** r/projectmanagement, [Getting client approvals](https://www.reddit.com/r/projectmanagement/comments/1lg5afj/getting_client_approvals/), retrieved page shows “1y ago.” Self-reported approval practices; response selection is not a representative survey.
- **EX-B05:** Shawn D. Belling, [When agendas override objectives: an information technology project vendor management case study](https://www.pmi.org/learning/library/case-project-history-statement-work-6871), PMI Global Congress, 24 February 2010. Firsthand client/vendor case; one retrospective account, not a fixed-price Scrum comparison study.
- **Repository context:** [Product blueprint](../../product.md), [cross-feature requirements](../../requirements.md), original [contracting](../02-pre-sales-and-contracting.md), [Scrum operating model](../04-scrum-operating-model.md), [offshore communication](../07-offshore-communication.md), and [pitfalls](../09-lessons-learned-and-pitfalls.md). Existing research and proposed practices are inputs, not empirical validation of these personas.
