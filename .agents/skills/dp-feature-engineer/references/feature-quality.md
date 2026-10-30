# Feature requirements review checklist

Open only while drafting/validating a feature; do not duplicate the product documents here.

## Discovery
- Actor and outcome are real needs, not framework/API assumptions.
- In/out of scope and client visibility are explicit.
- Similar capabilities in the roadmap or existing features are identified.
- Important assumptions cite their source or remain labeled `Open`.

## UR quality
- Describes a recognizable human/business need and desired measurable outcome.
- No invented external interview, legal approval or technical implementation.
- Critical unknowns (authority, policy, privacy, owner transfer) are surfaced.

## FR quality
- One coherent, independently testable behavior per FR.
- Source UR references are present; if directly derived from BR/NFR, explain why.
- Includes authorization, validation, state, error, edge/concurrency when relevant.
- Does not prescribe HTTP routes, EF entities or code layers without user-imposed technical constraint.

## US quality
- User-valued increment, independent enough for team refinement.
- Sources FR(s) and provides concrete Given/When/Then outcome(s).
- Scenarios do not assume client approval unless policy requires it.
- Does not turn technical infrastructure work into fake user benefit.

## Completion checks
- No duplicate ID; all references resolve.
- Reverse coverage gaps are reported, not silently filled with junk stories.
- No self-approval: approvals must reference independent evidence.
- The new files are drafts; open decisions and candidate scope are labeled.
- `python tools/docs.py validate --feature ...` succeeds and `coverage` is reviewed.
