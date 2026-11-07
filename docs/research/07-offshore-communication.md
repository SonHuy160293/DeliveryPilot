# 07 — Communication with offshore teams

[Research index](README.md) · [Previous: Risk, quality, and governance](06-risk-quality-and-governance.md) · [Next: Templates and checklists](08-templates-and-checklists.md)

**Status:** Research-supported guidance with an illustrative London–Vietnam working agreement. Times, response expectations, and meeting lengths are example team agreements rather than contractual defaults.

## Design communication around decisions and dependencies

An offshore delivery plan must account for the time needed to obtain a decision, not just the hours needed to implement it. A developer's question sent at the end of the Vietnam day may otherwise consume an additional working day before the client and team can resolve it together.

PMI's virtual-team guidance recommends treating asynchronous discussions as managed decision processes and accounting for time zones, shared calendars, and personal constraints. GitLab's documented remote practice similarly favors written asynchronous communication and capturing the conclusions of offline discussions. These are useful foundations; the Atlas agreement below adapts them to a client–vendor contract. [Sources: PMI](https://www.pmi.org/learning/library/managing-virtual-teams-high-performance-7310), [GitLab](https://handbook.gitlab.com/handbook/communication/).

## Agree the overlap before committing to response times

Assume the Atlas client works 09:00–17:00 in London and the vendor team works 09:00–18:00 in Vietnam, with breaks handled locally. Vietnam uses UTC+7 throughout the year. London changes between GMT and British Summer Time; the UK clock changes in 2026 occur on 29 March and 25 October. [Sources: Vietnam time](https://www.timeanddate.com/time/zone/vietnam), [UK clock changes](https://www.gov.uk/when-do-the-clocks-change).

| Proposed arrangement | London on GMT | London on BST | Practical implication |
|---|---|---|---|
| Normal-hours overlap | 09:00–11:00 London / 16:00–18:00 Vietnam | 09:00–12:00 London / 15:00–18:00 Vietnam | GMT leaves two overlapping hours; BST leaves three. |
| Daily reserved decision window | 09:00–10:00 London / 16:00–17:00 Vietnam | 09:00–10:00 London / 15:00–16:00 Vietnam | Keep the London window stable and update the Vietnam calendar when clocks change. |
| Example 15-minute Developers' Daily Scrum | 09:00–09:15 London / 16:00–16:15 Vietnam | 09:00–09:15 London / 15:00–15:15 Vietnam | Use this if Developers are distributed across both sites. A team entirely in Vietnam can choose a local time. |

Use `Europe/London` and `Asia/Ho_Chi_Minh` time zones in calendar invitations rather than a fixed offset copied into recurring meeting text. Show both local times in the team agreement. Record each site's holidays, planned leave, and release blackout periods. Review the schedule before clock changes and before major holiday periods such as Tet.

The reserve is availability for decisions, not a daily compulsory hour-long call. Protect concentrated work. For a workshop exceeding normal overlap, agree an occasional shifted schedule in advance and share the inconvenience. Do not imply that fixed price purchases unlimited out-of-hours access.

## A workable two-week calendar

Use the Scrum event purposes in [Chapter 04](04-scrum-operating-model.md). This example avoids requiring client attendance at every technical discussion.

| When | Meeting or artifact | Participants | Output |
|---|---|---|---|
| Each working day | Developers' Daily Scrum; 15 minutes | Developers; other roles only as appropriate to their work | Adjusted daily plan and impediments needing help. |
| Each working day before overlap | Written decision queue | Proxy/BA maintains; client PO reviews | Ranked questions with options, impact, and required-by times. |
| Sprint day 1 | Planning; reserve two hours after the Daily Scrum | Scrum Team; specialists when needed | Sprint Goal, selected work, plan, and visible dependencies. |
| Two short slots per week | Refinement; about 30 minutes each | PO/proxy and relevant Developers | Clearer examples, splits, and dependency decisions. |
| Weekly | Written status report; optional 30-minute exception call | Vendor PM, client PO/PM | Forecast, scope movement, risks, and decisions requiring action. |
| Sprint final day | Review, then retrospective; about one hour each | Review: team/stakeholders; retrospective: Scrum Team | Feedback and next priorities; an owned improvement action. |
| Milestone submission | Separate acceptance review if required | Named client acceptor and relevant evidence owners | Acceptance, rejection against criteria, or permitted conditional acceptance. |
| Monthly or on escalation | Steering review | Client sponsor, vendor engagement lead, PM, PO as needed | Decisions beyond delivery authority. |

On planning and final-review days, distributed Developers retain the 09:00–09:15 London Daily Scrum. Schedule planning at 09:15–11:15, or review at 09:15–10:15 followed by retrospective at 10:15–11:15. In GMT this ends at 18:15 Vietnam, so agree a 15-minute shift of the vendor working day on those dates in advance; in BST it ends at 17:15 Vietnam within normal hours. If the shift is unavailable, agree shorter local event durations that still meet their purpose or another explicit arrangement; do not double-book the Daily Scrum. If a larger review is needed, arrange additional availability rather than dropping the retrospective. Avoid routinely scheduling the sponsor meeting in the only slot available for team decisions.

## Choose tools by the records they must preserve

| Information | Required capability and authoritative record | Use of chat/video |
|---|---|---|
| Backlog and defects | Stable IDs, status history, comments, acceptance examples, evidence links, controlled access | Alert people to a record; return decisions to it. |
| Scope and contract changes | Versioned baseline, impact analysis, approver identity, decision time, and immutable accepted revision | Discuss options live; preserve the authorized decision in the agreed register. |
| Designs and working agreements | Shared documents with history, owners, and review dates | Use short demonstrations to explain complex changes. |
| Code, builds, and tests | Controlled repository, review history, artifact identifiers, and test results | Link the exact build when reporting a failure. |
| Meetings and decisions | Agenda, attendees, decisions, actions, owners, due dates; recordings where agreed | Use live calls for ambiguity, negotiation, and conflict resolution. |
| Incidents and escalation | Named support route, severity, acknowledgement, next update, and responsible owner | Use a dedicated urgent channel with agreed coverage. |

A procurement discussion should test client access, audit/export needs, permissions, retention, and integration before choosing a product. Having the same facts copied into several tools is less useful than a clear authoritative record with links.

GitLab's meeting guidance emphasizes balancing asynchronous and synchronous work. Apply that by moving ambiguous or circular exchanges to a short call, then documenting the decision so absent people can act. [Source: GitLab meeting guidance](https://handbook.gitlab.com/handbook/company/culture/all-remote/meetings/).

## Make asynchronous handoffs executable

Use this proposed format for a blocker or decision request:

```text
Item: ATLAS-41 — requester notification behavior
Build / environment: candidate identifier and test environment
Current evidence: baseline wording, test result, and reproduction link
Decision needed: notify on every status change, or only selected transitions?
Options: A — all agreed transitions; B — named subset with examples
Recommendation: A, because it matches the accepted workflow reference
Impact if unanswered: notification test completion blocked; milestone at risk
Decision owner: client PO; commercial owner if baseline changes
Required by: full date, local time, and time-zone name
Safe next action: continue unrelated permission tests
Answer / evidence: selected option, scope interpretation, approver, timestamp
```

Use an actual date and reference before sending the request. An unanswered question should never be quietly converted into a client-approved requirement.

**Proposed response agreement:** Acknowledge ordinary decision requests by the next shared working window and give a decision or a committed answer date. The PM escalates a blocked milestone when the required-by time is missed. Production incidents use their separately contracted response route and coverage hours. The request's timer uses the agreed calendars and does not run through a client's holiday unless explicitly contracted.

## Status reports that lead to action

The weekly message should fit on one screen, with links for evidence:

- **Outcome:** What customer workflow is now usable? Identify completed scope and candidate/build.
- **Forecast:** Is the next milestone still achievable? State the date, confidence, and changed assumptions.
- **Commercial position:** Agreed contract price, approved changes, payments/milestones, and any decision affecting cost. Keep the vendor's internal cost/margin detail in the appropriate audience view.
- **Quality:** Acceptance progress, serious defects, and relevant unresolved evidence.
- **Risks and decisions:** List the few items requiring attention, each with an owner, impact, and required-by date.
- **Next period:** Planned outcomes, client inputs needed, and upcoming acceptance events.

Avoid a report that only says "80% complete." A useful Atlas statement is: "The request-tracking slice meets its test criteria; milestone acceptance is pending the authorized client's review. The notification mapping decision is due in tomorrow's overlap; a delay would affect the next candidate." The state and required action are visible.

Use the detailed [sprint report template](08-templates-and-checklists.md) as the underlying record. Separate delivery progress from acceptance and payment status.

## Demo and acceptance meetings

Send the candidate version, agenda, intended scenarios, access instructions, and evidence links before the session. Invite the actual users for feedback and the named acceptor when a contractual decision is expected.

During the demo, run the agreed workflow with realistic data, include relevant error or permission behavior, and explain known limitations. Ask the client to state what they observed and what decision follows. Put a new feature request into the change process with its original wording preserved.

For an acceptance meeting, record the package/version, criteria considered, failures or exceptions, authorized decision, and any remaining obligations. Publish the record to attendees and absent decision owners. A recording supplements this record; attendance or silence does not automatically constitute acceptance.

Use clear language, avoid idioms, and ask people to restate important business rules in their own words. Give quieter participants a written route to raise concerns. If disagreement becomes personal or repetitive, the PM and Scrum Master should arrange a focused conversation and close it with a written decision or escalation.

## Sources

Accessed 7 November 2026. Recommendations, schedules, templates, and response thresholds are original illustrative arrangements.

1. George Pitagorsky, [Managing virtual teams for high performance](https://www.pmi.org/learning/library/managing-virtual-teams-high-performance-7310), PMI Global Congress paper, 2007. Supports deliberate asynchronous decision processes and time-zone/calendar planning; used for durable coordination principles rather than current tool recommendations.
2. GitLab, [GitLab Communication](https://handbook.gitlab.com/handbook/communication/), living handbook. Supports asynchronous communication and documenting offline conclusions.
3. GitLab, [All-Remote Meetings](https://handbook.gitlab.com/handbook/company/culture/all-remote/meetings/), living handbook. Supports deliberate use of synchronous and asynchronous work.
4. GOV.UK, [When do the clocks change?](https://www.gov.uk/when-do-the-clocks-change), updated 24 November 2025. UK clock-change rules and 2026 dates.
5. Time and Date AS, [Time Zones in Vietnam](https://www.timeanddate.com/time/zone/vietnam), live reference. Vietnam UTC+7 and absence of daylight-saving changes; this specialist time reference supports the schedule calculations.
