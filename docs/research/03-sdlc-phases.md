# 03 — SDLC phases from discovery to maintenance

[Research index](README.md) · [Previous: Contracting](02-pre-sales-and-contracting.md) · [Next: Scrum operating model](04-scrum-operating-model.md)

**Status:** Research-supported guidance with an illustrative operating model. All durations, thresholds, responsibilities, and commercial gates below are proposed arrangements for the fictional Atlas engagement, not universal Scrum requirements.

## Read the lifecycle as overlapping work and explicit decisions

A phase tells the PM which decision must be supported next. It should not create a queue in which analysis finishes before engineering starts, or engineering finishes before QA starts. Research, refinement, integration, documentation, and risk management continue throughout delivery.

The six Atlas development sprints identify the planned feature-delivery window. If the same team continues using Scrum during UAT or warranty, continue its cadence and useful goals; the commercial phase names do not create special Scrum events or justify deferring product quality work.

GOV.UK's discovery guidance supports investigating the problem and constraints before committing to a build, including the possibility of stopping. Its live-service guidance emphasizes ongoing operation and improvement. This chapter adapts those principles into an outsourcing lifecycle; the seven phases below are not the GOV.UK phase taxonomy. [Sources: discovery guidance](https://www.gov.uk/service-manual/agile-delivery/how-the-discovery-phase-works), [live-service guidance](https://www.gov.uk/service-manual/agile-delivery/how-the-live-phase-works).

For Atlas, two weeks of discovery cost **$15,000 under a separate agreement**. The subsequent **$180,000 fixed-price delivery** has a 16-week planning baseline, followed by a 30-calendar-day warranty from production release. Maintenance needs a separate agreement. These are example assumptions, not recommended benchmarks.

| Phase | Atlas timing | Main decision/evidence | Client decision owner | Vendor delivery owner |
|---|---|---|---|---|
| Discovery | Two weeks before delivery | Proceed, reshape, investigate further, or stop | Sponsor, advised by client PO | Engagement lead with BA/technical lead |
| Inception / Sprint 0 | Delivery week 1 | Delivery baseline and working arrangements accepted | Sponsor for contract; PO for product detail | PM, supported by Scrum Master and team |
| Development sprints | Weeks 2–13; six two-week sprints | Useful increments; updated forecast; agreed milestone evidence | PO for product decisions; named acceptor for milestones | Developers for delivery; PM for commercial tracking |
| Stabilization / UAT | Weeks 14–15 | Business acceptance of the identified release candidate | Client UAT lead and authorized acceptor | QA lead and team; PM coordinates |
| Release | Week 16 | Authorized production deployment and service verification | Client service owner/change authority | Release/DevOps lead |
| Warranty and handover | Handover in week 16; warranty for 30 calendar days from release | Operational ownership transferred; warranty obligations tracked to closure | Client operations owner and contract owner | Support lead and PM |
| Maintenance | Separately agreed start; may overlap warranty | Funded service ownership and ongoing improvement | Service/product owner | Maintenance service manager |

Payment gates are described in [Contracting](02-pre-sales-and-contracting.md). Calendar progress alone does not satisfy them. If a gate fails, record the actual evidence, impact, owner, and next decision date; update the forecast without retrospectively rewriting the original baseline.

## 1. Discovery

**Purpose:** Establish whether the opportunity is worth funding and whether enough uncertainty has been resolved to price the next commitment.

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Discovery agreement and research questions agreed; sponsor and PO named; access to representative users and existing-system owners scheduled. |
| Activities | Interview users; map the service journey; test the problem and success measures; inspect integration documentation; identify security, data, accessibility, and operational constraints; explore alternative scope boundaries; test high-risk assumptions through timeboxed investigation. |
| Deliverables | Problem/outcome brief; user journey; initial backlog; integration/dependency map; assumptions and exclusions; risk register; estimate range; recommended delivery option and evidence gaps. |
| Client responsibilities | Supply domain experts and representative workflows; disclose constraints; arrange third-party contacts; validate business outcomes; decide whether and what to fund. |
| Vendor responsibilities | Facilitate research; challenge ambiguous scope; investigate feasibility; explain estimate confidence and limitations; propose a priced next step only when its assumptions are visible. |
| Exit criteria | Sponsor records proceed/reshape/stop decision; major unknowns have evidence or an explicitly funded investigation plan; proposed scope, exclusions, and estimate assumptions are understood. |
| Common blockers | No access to users; undocumented interfaces; an imposed solution without a clear problem; unresolved data ownership. Name an owner and due date before treating any of these as settled. |

Do not turn an untested integration assumption into a supplier promise simply because the bid deadline arrives. In the Atlas example, an unavailable identity-provider sandbox could lead to another discovery activity or a separately priced integration option.

## 2. Inception / Sprint 0

**Purpose:** Make the agreed delivery commitment executable. "Sprint 0" is a practitioner label for preparation, not a defined Scrum event; the Scrum.org discussion explicitly identifies its informal status. Atlas calls this its one-week inception period. [Source: Derek Davidson, Scrum.org](https://www.scrum.org/resources/blog/truth-about-sprint-zero-and-why-ken-hates-it).

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Delivery SOW signed; funding and team availability confirmed; discovery gaps classified; client decision rights recorded. |
| Activities | Kick off; reconcile the baseline backlog against the SOW; refine the first slices; establish acceptance examples and DoD; provision repositories/environments; prove a minimal deployment path; agree reporting, change control, and UAT participation. |
| Deliverables | Versioned baseline and release forecast; prioritized backlog; initial architecture and integration decisions; working agreement; quality/test approach; access inventory; decision and escalation matrix; initial UAT plan. |
| Client responsibilities | Approve the baseline through the named authorities; provide access, test data owners, and UAT representatives; confirm PO availability and escalation deputies. |
| Vendor responsibilities | Assemble the team; expose resource assumptions; prepare environments; establish build/test automation; make the first sprint's dependencies visible. |
| Exit criteria | First slice can be started and demonstrated with agreed evidence; critical access exists or an approved workaround is credible; baseline and responsibilities have version-specific acceptance. |
| Common blockers | Environment procurement; no empowered client PO; stakeholders disagree about mandatory scope; architecture effort expands without a time limit. |

The first $36,000 delivery milestone is tied to the agreed inception outputs. It does not imply that all later stories are fully specified. If setup takes longer, show the consequence to the delivery baseline instead of hiding extra preparation inside sprint capacity.

## 3. Development sprints

**Purpose:** Deliver and inspect small usable slices, learn early, and keep the completion forecast credible. Apply the Scrum accountabilities and events in [Chapter 04](04-scrum-operating-model.md).

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Delivery baseline established; a useful next goal is available; team capacity and major dependencies are understood. Each sprint starts with planning for the current evidence. |
| Activities | Refine, design, implement, review, test, integrate, and document; demonstrate completed slices; collect user feedback; analyze changes; maintain risks and forecasts; improve the delivery process. |
| Deliverables | Versioned increments; automated/manual test evidence; updated backlog; deployment artifacts; decisions and change records; sprint report; forecast and risk updates. |
| Client responsibilities | Answer product questions; make ordering/trade-off decisions; join reviews with relevant users; accept milestone packages through the agreed process; provide dependencies on time. |
| Vendor responsibilities | Deliver to the quality agreement; surface blockers early; maintain traceable evidence; forecast remaining work and cost; distinguish defects from proposed new scope. |
| Exit criteria | At each sprint end, report what is genuinely complete, what remains, and what was learned. Enter UAT only when its release-candidate criteria are met; the six-sprint forecast alone is insufficient evidence. |
| Common blockers | Late clarification; inaccessible third-party systems; partially integrated work reported as finished; client feedback treated as unlimited scope; defects accumulating outside the forecast. |

Atlas's next payment packages are $36,000 after the accepted Sprint 2 slice and $45,000 after the accepted Sprint 4 slice. If a slice fails its agreed acceptance, the PM follows the rework/dispute provisions rather than declaring acceptance based on a successful demo.

## 4. Stabilization and UAT

**Purpose:** Validate the complete business workflow and operational readiness of a specific release candidate. This is additional assurance; sprint work already includes testing and integration.

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Candidate build identified; included items meet DoD; critical integration tests pass; realistic UAT data/environment exist; client testers and acceptance scenarios are booked; known defects are disclosed. |
| Activities | Run end-to-end business scenarios; check roles and data reconciliation; rehearse migration and recovery; complete agreed performance/security evidence; triage findings daily; fix and retest defects; review training and support materials. |
| Deliverables | UAT execution record; defect log; retest evidence; release-candidate identifier; operational readiness checklist; signed acceptance/rejection decision with conditions and owners. |
| Client responsibilities | Execute or witness business acceptance tests; make business-severity decisions; identify material acceptance failures; sign through the authorized acceptor. |
| Vendor responsibilities | Provide a stable candidate and evidence; support testers; reproduce and classify findings; fix contractual defects; explain changes needing commercial authorization. |
| Exit criteria | Agreed scenarios pass, subject only to permitted, explicitly recorded minor exceptions; illustrative Atlas gate: no open critical or major defects; exceptions have named owners, dates, and explicit acceptance; authorized UAT decision recorded. |
| Common blockers | Testers unavailable; production-like data arrives late; newly requested behavior is mixed into defect counts; repeated candidate changes invalidate earlier evidence. |

Use the [UAT sign-off template](08-templates-and-checklists.md). UAT approval triggers the illustrative $45,000 milestone. Conditional acceptance must state exactly which obligations remain; it is not an implied waiver of defects.

## 5. Release

**Purpose:** Deploy an accepted candidate and establish that the live service works and can be supported. GOV.UK's live guidance identifies operational preparation, monitoring, security, performance, and continuing QA as readiness concerns; the specific gate below is Atlas's proposed implementation. [Source: live-service guidance](https://www.gov.uk/service-manual/agile-delivery/how-the-live-phase-works).

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | UAT decision available; production access and release window approved; rollback and recovery rehearsed; monitoring, support contacts, and communications prepared. |
| Activities | Conduct go/no-go review; deploy the identified artifact; migrate/reconcile data; smoke-test critical workflows; monitor errors and service health; execute rollback if agreed triggers are crossed. |
| Deliverables | Deployment record; artifact/version references; migration results; production verification evidence; known-issue notice; release notes; recorded production start time. |
| Client responsibilities | Authorize the business release; arrange business communications and operations coverage; approve service impact; verify critical live workflows. |
| Vendor responsibilities | Execute the release plan; preserve audit evidence; monitor the deployment; recover or roll back when necessary; communicate actual service status. |
| Exit criteria | Live verification passes, operations accepts service ownership, and monitoring/support routes work. Production release starts the example warranty clock. |
| Common blockers | Untested rollback; missing operational owner; incorrect permissions; unverified migration; a calendar date treated as overriding failed safety criteria. |

If production deployment succeeds but the agreed handover package is incomplete, the combined $18,000 final delivery milestone remains subject to its contractual conditions.

## 6. Warranty and handover

**Purpose:** Transfer usable ownership and fulfill the agreed defect remedy. Start knowledge transfer during development so it can finish around release.

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Handover inventory and recipients agreed before release; warranty starts on the recorded production release date; support intake and severity rules active. |
| Activities | Transfer source/build instructions, deployment ownership, runbooks, dependency/licenses inventory, access responsibilities, and support knowledge; demonstrate restore/redeploy; triage and repair covered defects; track unresolved obligations. |
| Deliverables | Handover acceptance; recorded training; source/artifact references; operational runbooks; access transfer/revocation evidence; warranty log; closure or continuing-obligation record. |
| Client responsibilities | Nominate competent recipients; verify access and operational exercises; report reproducible defects; accept handover evidence; fund maintenance arrangements. |
| Vendor responsibilities | Deliver the agreed transfer package; remove temporary access as agreed; repair covered defects; keep warranty and paid enhancement work distinguishable; retain traceable closure evidence. |
| Exit criteria | Client accepts the complete handover; warranty period has elapsed; claims raised in time are closed or explicitly tracked under the agreed continuing obligations. In the proposed Atlas terms, period expiry is not treated as closure of an unresolved covered claim. |
| Common blockers | Documentation delivered without a working exercise; licensing restrictions discovered late; no recipient team; disputes over configuration changes versus product defects. |

Atlas uses 30 calendar days from production release as its illustrative warranty period. Define covered defects, reporting procedure, remedy expectations, exclusions, and treatment of timely claims in the actual contract. Warranty is not an assumed 24/7 operations service.

## 7. Maintenance

**Purpose:** Sustain the service and fund further improvements under an explicit operating agreement.

| Element | Atlas operating arrangement |
|---|---|
| Entry criteria | Maintenance scope, service hours, funding, ownership, access, response expectations, and warranty interface agreed. Maintenance can begin during warranty with clear routing. |
| Activities | Monitor; manage incidents; patch dependencies; verify backups; review capacity/security; perform problem analysis; prioritize enhancements; plan ongoing releases. |
| Deliverables | Service reports; incident/problem records; maintenance backlog; patch/release evidence; renewal and capacity recommendations. |
| Client responsibilities | Own business priorities and service funding; approve planned changes and outages; maintain client-controlled dependencies; evaluate service outcomes. |
| Vendor responsibilities | Provide contracted coverage; meet agreed response/restore targets; identify recurring problems; maintain operational evidence; price changes beyond service scope. |
| Exit criteria | For an ongoing service, each review confirms performance and next commitments. At termination, agree transition, data/access transfer, retained obligations, and final service acceptance. |
| Common blockers | Warranty and support billed inconsistently; unfunded enhancement requests; unsupported dependencies; missing incident ownership; knowledge lost when the delivery team leaves. |

## Sources

Accessed 7 November 2026. The phase tables, Atlas figures, and gate designs are original practitioner examples informed by these sources; they are not reproduced standards.

1. Government Digital Service, [How the discovery phase works](https://www.gov.uk/service-manual/agile-delivery/how-the-discovery-phase-works), Service Manual. Supports problem/constraint research and an evidence-based decision to proceed or stop; its public-service phase names differ from this chapter.
2. Government Digital Service, [How the live phase works](https://www.gov.uk/service-manual/agile-delivery/how-the-live-phase-works), Service Manual. Supports operational readiness and continuing improvement after launch.
3. Derek Davidson, [The Truth About Sprint Zero (and why Ken hates it)](https://www.scrum.org/resources/blog/truth-about-sprint-zero-and-why-ken-hates-it), Scrum.org, 20 September 2021. Practitioner explanation of the informal Sprint 0 label.
