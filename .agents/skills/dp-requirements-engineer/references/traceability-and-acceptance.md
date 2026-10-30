# Requirement traceability and acceptance

## Minimum feature contract

A good `docs/features/<feature-id>/requirements.md` identifies the proposed problem and scope, primary actors, uniquely numbered functional requirements, business rules, negative paths, linked nonfunctional constraints, and open approvals. Link to user research rather than duplicating it.

Use a Markdown table with the FR ID in the first column; the validator detects declared rows such as:

| ID | Proposed behavior | Rationale | Approval |
|---|---|---|---|
| FR-DP-001 | Authorized manager can inspect evidence underlying a possible blocker | Explainable recommendations | Proposed |

Match acceptance scenario references **explicitly**:

### AC-01: FR-DP-001 - visible evidence

- **Given** a manager authorized for a project and a finding with provenance
- **When** the manager opens the finding
- **Then** source reference and freshness are visible without exposing another tenant's information

### AC-02: FR-DP-001 - tenant isolation

- **Given** a user belonging only to tenant A
- **When** that user requests details of tenant B's finding
- **Then** details are not disclosed and access is audited if required by existing policy

Use accepted numerical targets only when their evidence and owner exist. Include `FR-*` references in acceptance sections so automatic checks can spot uncovered requirements.

## Additional quality checks

- FR describes observable user/business behavior, not a class or SQL implementation.
- Every FR has provenance and acceptance evidence *to be produced*; do not claim tests already passed.
- Alternative and exception flows cover ambiguous results, stale dependencies, duplicates, concurrency and revoked access as appropriate.
- User story focuses on actor/outcome; use case includes main and exception paths; both link to FR IDs.
- 
Avoid vague requirements such as "fast", "easy" or "reliable" without measurable predicates or explicitly pending decisions.
- Production status `accepted` requires independent approval evidence (`approval_ref`), not an AI-generated statement.
