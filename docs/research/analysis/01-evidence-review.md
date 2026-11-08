---
id: RESEARCH-DP-ANALYSIS-01
title: "01 — Evidence review and limits"
status: proposed
---

# 01 — Evidence review and limits

[Analysis index](README.md) · [Next: Personas](02-stakeholders-and-personas.md)

The evidence supports investigating agreement integrity and client/vendor decision coordination. It does **not** establish the frequency of each pain among present-day fixed-price Scrum projects, willingness to pay, or whether a new platform outperforms a well-configured existing toolchain. Product features and priority rankings below remain **AI-inference** unless explicitly attributed to the owner.

## Original research coverage

These are **repository-verified** inputs. They synthesize practices; they are not eleven independent empirical studies.

| Original file | Contribution to product definition | Boundary or follow-up |
|---|---|---|
| [00 Plan](../00-research-plan.md) | Original topic coverage and research intent | Planning is not outcome evidence |
| [01 Overview](../01-overview.md) | Distinguish fixed-price agreements, flexible scope and commercial hybrids | Initial product targets signed fixed-price engagements, not every hybrid |
| [02 Contracting](../02-pre-sales-and-contracting.md) | Agreed scope, assumptions, exclusions, contingency, milestones and CR clauses | Import/reference signed outputs; do not build presales or legal drafting workflows initially |
| [03 SDLC phases](../03-sdlc-phases.md) | Actors, deliverables and phase gates through maintenance | Reconcile to eight product stages and repeated release/UAT loops |
| [04 Scrum model](../04-scrum-operating-model.md) | Accountability, events, readiness, Done and contractual acceptance distinctions | Keep Daily Scrum and timebox integrity; approval is not universal for every story |
| [05 Scope/change](../05-scope-and-change-management.md) | Baseline changes, swaps, budget visibility and EVM examples | Version-bound decisions and privacy; story points are not currency |
| [06 Governance](../06-risk-quality-and-governance.md) | Risk owners, QA, escalation and reporting | Health needs evidence and freshness, not just an opaque score |
| [07 Offshore collaboration](../07-offshore-communication.md) | Availability, asynchronous decisions, demos and communication agreements | Test client response windows and timezone/calendar assumptions |
| [08 Templates](../08-templates-and-checklists.md) | SOW, CR, report, UAT and kickoff fields | Translate fields into workflow/evidence; avoid mandatory forms without purpose |
| [09 Pitfalls](../09-lessons-learned-and-pitfalls.md) | Failure mechanisms and preventative practices | Candidate risks/interview probes, not measured prevalence |
| [README](../README.md) | Reading order and shared example context | Preserve industry research separately from current product scope |

The Atlas project is a **fictional teaching example**: $180,000 delivery price, $120,000 base delivery cost plus $18,000 contingency, six two-week Sprints within a 16-week delivery plan, and a separate discovery fee. Its figures illustrate accounting/forecast distinctions; they are not estimates for building DeliveryPilot or evidence of typical margins. The current product's Sprint example is independently labelled illustrative.

## External evidence register

Access/review date: 2026-11-07. “No date” means the retrieved page did not establish a publication date. Source IDs used in earlier chapters are retained.

| ID / source | Type and relevant observation | Limit and product implication |
|---|---|---|
| EX-C01 — [Scrum Guide](https://scrumguides.org/scrum-guide.html), November 2020 | Framework authority: fixed-length Sprints and separate accountabilities | Governs Scrum terminology; does not prescribe outsourcing payment gates or story points |
| EX-B01 — [Hoda, Noble, Marshall](https://rashina.com/wp-content/uploads/2011/06/hoda-ist2011-impactcustinvol.pdf), 2011 | Qualitative research on inadequate customer collaboration | Older sample of 30 practitioners/16 organizations; supports collaboration hypotheses, not current frequency estimates |
| EX-B02 — [Dave West, Scrum.org](https://www.scrum.org/resources/blog/working-web-development-company-using-scrum), 2016-02-19 | Firsthand client account of choosing an agency and collaboration model | Rolling Sprint capacity rather than fixed total scope/price; use as a participation example, not outcome proof |
| EX-B03 — [UAT discussion](https://www.reddit.com/r/projectmanagement/comments/16zg8o5/when_in_the_project_lifecycle_would_you_aim_to_do/), relative timestamp | Client participant describes late involvement and compressed UAT/training | Anonymous, contract unknown; motivates questions about readiness and client capacity |
| EX-B04 — [Client approvals discussion](https://www.reddit.com/r/projectmanagement/comments/1lg5afj/getting_client_approvals/), relative timestamp | Practitioner reports saving email approvals; discussion includes other approaches | Anecdotal, self-selected; simple email remains a credible alternative |
| EX-B05 — [Belling, PMI vendor-management case](https://www.pmi.org/learning/library/case-project-history-statement-work-6871), 2010-02-24 | Client-side retrospective on authority and competing agendas | Earlier analysis reviewed it; latest retrieval timed out. One historical case, not prevalence or evidence that software fixes politics |
| EX-C02 — [GOV.UK discovery guidance](https://www.gov.uk/service-manual/agile-delivery/how-the-discovery-phase-works), maintained guidance | Discovery investigates needs/constraints before committing to build | Public-service context; upstream research guidance rather than a mandated product phase |
| EX-C03 — [Stripe invoice overview](https://docs.stripe.com/invoicing/overview), maintained documentation | Example of external invoice document/collection lifecycle | Provider-specific behavior; native financial processing remains excluded |
| EX-A01 — [Deloitte 2024 Global Outsourcing Survey](https://www.deloitte.com/us/en/services/consulting/articles/global-outsourcing-survey.html), 2024 report | Broad outsourcing context, including governance and relationship management | Cross-service executive evidence; cannot rank fixed-price Scrum pains or validate this product |
| EX-A02 — [PMI Pulse of the Profession 2024](https://www.pmi.org/-/media/pmi/documents/public/pdf/learning/thought-leadership/pmi-pulse-of-the-profession-2024-report.pdf), 2024 | Cross-industry report on delivery approaches and organizational enablers | Not a controlled comparison of outsourcing contract models; supports considering context rather than one universal workflow |
| EX-A03 — [Atlassian approvals documentation](https://support.atlassian.com/jira-service-management-cloud/docs/what-are-approvals/), no date | Existing service-management tools support approval steps | Counterexample to “trackers cannot do approvals”; compare configured alternatives in pilots |
| EX-A04 — [OpenAPI Specification](https://spec.openapis.org/oas/latest.html), maintained specification | Standard format for describing API interfaces | Does not establish deployment availability, consumer integration success or performance results |
| EX-A05 — [GOV.UK in-depth interviews](https://www.gov.uk/service-manual/user-research/using-in-depth-interviews), maintained guidance | Methodological support for probing experience and context | Does not validate our personas or recruitment sample |

## Synthesis and counterevidence

**AI-inference:** Start product discovery around a disputed commitment: what version was agreed, what changed, who can decide, and what evidence they need. This connects the research more directly to product behavior than a generic task board.

The strongest counterexamples are already in the evidence: existing tools can approve requests; email can preserve a simple agreement; an engaged client PO can resolve uncertainty directly; a sponsor may refuse to act despite complete records. Accordingly, DeliveryPilot must demonstrate less reconciliation effort and clearer decision context, not claim that a portal inherently fixes collaboration.

Unresolved evidence gaps: present-day fixed-price incidence, losses caused by each pain, client adoption cost, multi-employer usage, willingness to share budget data, real API/test traceability needs, and demand for AI versus deterministic reminders. These feed [Chapter 08](08-assumptions-and-interviews.md).

## Sources

The register above contains direct sources and their limits. Original repository sources are linked in the coverage table. Product-owner scope takes its authority from [D1–D6](../../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions), not from external reports.
