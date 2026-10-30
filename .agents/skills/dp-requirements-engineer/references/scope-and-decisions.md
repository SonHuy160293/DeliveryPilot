# Requirement authority and high-risk decisions

- Product owner / authorized stakeholder owns business policies, in/out scope, user-visible actions, retention, customer consent, commercial definitions and approval.
- Security owner owns authorization boundaries; treat tenant boundaries and data minimization as nonnegotiable unless explicitly changed through the authorized process.
- Solution Architect / DBA own technical strategy. Do not choose infrastructure or create database schemas in a user story.
- Source, tests, generated OpenAPI and migrations prove implemented behavior; they do not grant permission to change the business requirement.
- AI research is input to hypotheses. It cannot validate itself or approve an autonomous action.

For DeliveryPilot, explicitly consider:

1. Who can view and act on a risk finding across project and organization boundaries?
2. Is a suggestion read-only, or may the system modify external systems? If unknown, propose read-only behavior and request an owner decision; never implement side effects based on inference.
3. Is the source fresh enough to justify a blocker claim? What is displayed when unavailable?
4. Can a finding be explained, disputed, dismissed, or corrected? These are candidate requirements until accepted.
5. What are the privacy and failure modes of project-management connectors or AI processing?

Keep status `proposed` without a human approval reference even when all acceptance scenarios are drafted.
