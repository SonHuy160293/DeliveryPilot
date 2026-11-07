# 01 — Fixed-price outsourcing with Agile Scrum

[Research index](README.md) · Next: [Pre-sales and contracting](02-pre-sales-and-contracting.md)

**Evidence:** Research-supported concepts with practitioner recommendations and fictional examples. Sources checked 2026-11-07.

## The commercial problem to solve

A client wants a usable product within an authorized investment and business deadline. A vendor needs a defined obligation it can price and deliver at an acceptable margin. Both discover details during implementation: an integration behaves differently, a workflow needs another approval, or users prefer a simpler interaction.

A fixed price does not by itself freeze every feature or delivery date; the agreement determines which obligations are fixed. Under a firm fixed-price delivery obligation, the vendor normally absorbs its own estimation and execution overruns. The client's wish list does not automatically become additional contracted scope. This allocation is described in PMI's fixed-price contracting paper. ([Lowden and Thornton, 2015](https://www.pmi.org/learning/library/challenges-fixed-price-contracts-9640))

Scrum supports learning through short iterations, an evolving Product Backlog, and inspection of usable increments. It permits scope clarification during a Sprint while protecting the Sprint Goal and quality. ([Scrum Guide, 2020](https://scrumguides.org/scrum-guide.html))

The conflict appears when the commercial promise assumes that all details are knowable upfront while delivery requires decisions as evidence emerges. Calling the work Agile does not resolve that mismatch. The parties need an explicit way to accept learning, choose trade-offs, and authorize changes.

**Practitioner recommendation:** Agree the product objective, minimum usable outcome, mandatory constraints, acceptance evidence, and change mechanism before debating the name of the contract model. Then establish which variables may move and who may authorize that movement.

## Compare the available models

The following is a practitioner comparison of commercial choices. Rolling commitments and incentive clauses are discussed by Allan Kelly; capped expenditure and different combinations of constraints are explored by Evan Leybourn. These articles describe approaches, rather than prescribe a standard contract. ([Kelly, 2011](https://www.infoq.com/articles/agile-contracts/); [Leybourn, 2016](https://www.pmi.org/learning/library/measuring-value-10209))

| Model | What is fixed; what can change | Payment basis and risk allocation | Suitable situation and essential control |
|---|---|---|---|
| Fixed price for a defined deliverable | Price and agreed acceptance obligation; details may be clarified within the baseline, other changes require agreement | Milestone or delivery payments; vendor carries ordinary delivery cost overrun, client retains business-value risk and the dependency risks allocated to it | Repeatable work with bounded uncertainty; precise inclusions, exclusions, acceptance, and change control |
| Fixed price per sprint | Price for the named iteration; define whether it buys capacity or specified results; later backlog remains adaptable | Fee per authorized sprint; purchased capacity does not inherently guarantee every requested feature | Evolving needs with frequent client decisions; state capacity, delivery expectations, quality, and continuation/exit terms |
| Fixed price per phase or release slice | Price and deliverables for the current phase; later phases are estimated and authorized separately | Payment against phase evidence; later price and schedule remain open until agreed | Discovery before build, or separable releases; avoid implying that an initial phase price guarantees the entire product |
| Capped T&M | Rates and maximum authorized spend; scope is prioritized within the cap | Actual eligible time and expenses up to the ceiling; client carries more completion risk unless the contract expressly reallocates it | Uncertain scope with strong financial oversight; forecast cap exhaustion early and define stop, extend, or reduce-scope decisions |
| Fixed price with scope swapping | Total price and agreed commercial envelope; approved substitutions alter the feature mix | Existing payment structure; both parties assess the commercial effect of substitutions | Several optional features compete for capacity; evaluate dependencies and rework as well as size |
| Fixed price with early exit and scope swapping | Baseline agreement plus negotiated substitution and termination rights | Agreed payment for delivered work and an agreed exit settlement; incentives reward earlier value | Client can benefit from a partial product and both parties accept the exit economics; define settlement and transfer obligations |

A capped T&M ceiling is an expenditure limit. It becomes a completion guarantee only if the parties expressly accept that obligation. Similarly, buying six sprints of capacity and buying a specified product in six sprints are different promises. Write down which one applies.

## “Money for nothing, change for free”

Jeff Sutherland's 2008 article combines two mechanisms: a client can end the engagement at a Sprint boundary with an agreed settlement, and can substitute equal-scope work without increasing the price. His example uses 20% of the remaining contract value for early termination and makes flexibility conditional on client participation and agreed estimates. It also discusses fallback T&M and an 80% scope guarantee; these are features of that historical proposal, not default Scrum rules or terms automatically adopted here. ([Original article](https://jeffsutherland.com/scrum/2008/10/agile-contracts-money-for-nothing-and.html))

The practical attraction is incentive alignment. A client that has obtained enough value can avoid spending the entire remaining budget. A vendor can receive a settlement and redeploy capacity. Substitution gives the client a reason to prioritize actively instead of treating every discovery as a request for more funding.

**Illustrative alternative to Atlas's baseline contract:** Suppose an agreed early-exit clause defines the remaining value as $90,000 and the settlement as 20% of that amount. The exit payment is $18,000; the client avoids $72,000 of future charges. Previously earned, unpaid invoices remain separately due. The clause must address work in progress, prepaid amounts, third-party commitments, transfer of deliverables, and continuing warranty obligations. This example changes the commercial arrangement and is not an assumed Atlas entitlement.

For a scope swap, equal points alone are insufficient. Replacing an unstarted report with another report might preserve the estimate but introduce a paid data connector, a security review, or a new deployment dependency. Record the full impact, removal of the old obligation, acceptance criteria for the replacement, and authorization before treating the change as cost neutral. The detailed process is in [scope and change management](05-scope-and-change-management.md).

## A workable operating agreement

For the fictional Atlas service portal, this guide uses a separate two-week discovery engagement priced at $15,000, followed by a $180,000 fixed-price delivery engagement. Delivery lasts a planned 16 weeks: inception, six two-week development sprints, stabilization/UAT, and release/handover. Warranty lasts 30 calendar days from production release; maintenance is separately contracted. All amounts are USD and exclude taxes and pass-through costs.

The following proposed operating agreement makes that example manageable:

| Decision | Working rule | Evidence |
|---|---|---|
| What are we buying? | A defined portal release and measurable quality/service requirements | Versioned SOW, scope baseline, and acceptance matrix |
| What may the PO adjust? | Backlog order and clarifications within delegated authority | Backlog history and decision records |
| What changes the commercial promise? | New obligations, removed obligations, changed acceptance, price, or committed dates | Approved change request or documented contractual substitution |
| Who decides? | Client PO owns product priorities; named client and vendor commercial authorities authorize contract changes | Authority matrix and approval record |
| What counts as complete? | Agreed quality evidence plus the acceptance decision required by the contract | Test evidence, identified build/version, and authorized acceptance |
| What happens when the forecast slips? | Present options with cost, date, scope, and quality implications immediately | Updated forecast, issue record, and explicit decision |

The initial backlog is estimated at 120 story points for this team's planning. It is not a contractual currency. The client buys the documented deliverables; the vendor forecasts how to deliver them.

## Choose a model based on the uncertainty

Use a short commercial workshop before issuing the final proposal. Ask both parties to identify the least negotiable outcome and the largest unresolved assumption.

| Situation discovered | Recommended next step |
|---|---|
| Integration access and data quality are unknown | Price a discovery spike and obtain evidence before fixing the build obligation |
| Business deadline is firm but several features are optional | Define the minimum launch outcome and substitution/deferment authority |
| Buyer requires every mandatory feature and a firm ceiling | Validate feasibility, price the retained risks, and identify negotiated remedies for failed assumptions |
| Client cannot provide an empowered PO or UAT participants | Resolve decision capacity and acceptance ownership before accepting a schedule based on rapid feedback |
| Work cannot be usefully separated into small slices | Examine integration and acceptance dependencies; do not promise easy early exit without a usable handover |
| New evidence exceeds the agreed commercial envelope | Offer a priced change, a documented trade-off, or a negotiated replan |

These are practitioner decision prompts. No contract type removes uncertainty. A senior PM makes the remaining uncertainty visible, places it with an owner, and ensures that the team can obtain a decision before it consumes the available budget.

## Sources

- Ken Schwaber and Jeff Sutherland. [The Scrum Guide](https://scrumguides.org/scrum-guide.html), November 2020. Framework reference for empiricism, Sprint constraints, and incremental delivery. Accessed 2026-11-07.
- George Lowden and John Thornton. [The special challenges of project management under fixed-price contracts](https://www.pmi.org/learning/library/challenges-fixed-price-contracts-9640), PMI Global Congress paper, 11 May 2015. Fixed-price obligations and vendor cost risk. Accessed 2026-11-07.
- Evan Leybourn. [How much does this cost?](https://www.pmi.org/learning/library/measuring-value-10209), PMI Global Congress paper, 13 May 2016. Commercial constraints and capped T&M. This is an author's conference paper, not a PMI contract standard. Accessed 2026-11-07.
- Allan Kelly. [Agile Contracts](https://www.infoq.com/articles/agile-contracts/), InfoQ, 8 February 2011. Practitioner discussion of rolling commitments and contracting incentives. Accessed 2026-11-07.
- Jeff Sutherland. [Agile Contracts: Money for Nothing and Your Change for Free](https://jeffsutherland.com/scrum/2008/10/agile-contracts-money-for-nothing-and.html), 25 October 2008. Original historical clause proposal; percentages and conditions are contextual examples. Accessed 2026-11-07.
