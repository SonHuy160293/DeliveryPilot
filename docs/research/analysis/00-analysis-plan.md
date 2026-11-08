---
id: RESEARCH-DP-ANALYSIS-00
title: "00 — Analysis plan and research questions"
status: proposed
---

# 00 — Analysis plan and research questions

[Analysis index](README.md) · [Next: Evidence review](01-evidence-review.md)

This records the analysis scope requested and subsequently authorized by the product owner. It consolidates the plan for the existing analysis work; it is not a claim that this file existed before that approval. The latest documentation refactor is authorized by “implement the plan.” Product decisions D1–D6 narrow the initial product to post-contract fixed-price delivery.

## Objective and outline

Connect outsourcing SDLC research to a coherent product operating model: who acts, what they need to know, what evidence supports decisions, and what happens when agreement or delivery diverges. Read in this order: evidence → personas → phase needs → pains/gaps → lifecycle → object rules → capabilities/priorities → validation.

| Research question | Analysis output |
|---|---|
| Who owns business priorities, delivery coordination, acceptance and payment decisions? | 02: Six personas and delegated authority boundaries |
| What does each side do, decide or need at each moment? | 03: Phase needs and disputed-change journey |
| Which recurring problems are consequential, and how strong is the evidence? | 04: Severity, recurrence hypotheses, confidence and information gaps |
| How do teams work around missing context in existing tools? | 04: Tool/process alternatives and failure conditions |
| How does the eight-stage lifecycle support iterative delivery? | 05 and canonical workflow: gates per scope/release, repeat paths and actors |
| How do scope versions, CRs, Sprints, milestones, acceptance and finance evidence relate? | 06: State dimensions, guarded transitions and fixed-price rules |
| How can actual late work remain visible without extending a Sprint? | Canonical workflow: original plan, actual events, forecasts and worked example |
| Which product opportunities merit the first increment? | 07: MoSCoW proposals traced to pains and stable feature IDs |
| What could invalidate the design? | 08: Assumptions, interviews, comparative prototypes and decision criteria |

## Method and boundaries

1. Review all eleven files in the original research directory; preserve that material as industry context.
2. Triangulate normative Scrum guidance, qualitative collaboration research, client/vendor case accounts, broad reports, tool documentation and practitioner forums. Avoid treating them as equivalent evidence.
3. Mark claims using the evidence labels in the index. Report missing evidence instead of inventing interviews, frequency percentages or customer approval.
4. Separate product-owner decisions from inferred operating policy, market hypotheses and implementation reality.
5. Map the product to eight overlapping governance stages. Preserve original Sprint timeboxes; model actual execution separately.
6. Trace pains to opportunities and existing feature IDs. Keep detailed contracts and source code outside this refactor.
7. Check links, identifiers, examples and documentation structure. Structural validation cannot validate the business model.

## Files produced

README.md and 00–08 listed in the [index](README.md). Canonical changes are in `docs/product.md`, `docs/sdlc-workflow.md`, `docs/requirements.md`, `docs/roadmap.md`, the workflow decision record, context map, authoring ownership, root README and alignment audit. Research and canonical policy have separate owners to avoid conflicting copies.

## Sources

- User's requested analysis outline, later scope clarification and implementation authorization, recorded in [D1–D6](../../deliverypilot-workflow-discovery.md#197-lifecycle-and-execution-visibility-decisions).
- [Original research plan](../00-research-plan.md), [authoring policy](../../authoring.md) and [evidence register](01-evidence-review.md).
