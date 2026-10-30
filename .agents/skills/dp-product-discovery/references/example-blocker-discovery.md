# Example: candidate blocker-detection research (illustrative only)

User-provided intent: DeliveryPilot should detect blockers, track dependencies, and suggest actions before a manager notices delays.

Potential *hypotheses*, not approved product facts:

- H-01: Missing a promised dependency date may indicate a delivery risk.
- H-02: A manager benefits from seeing the underlying source and freshness of every suggested blocker.
- H-03: Automatic escalations could harm trust if false-positive rates are high.

Research questions:

1. How do teams distinguish waiting work from genuinely blocked work?
2. Which tools carry source-of-truth task/dependency relationships?
3. How often are delays discovered after they affect other work?
4. Which decisions can be suggested safely without changing source project-management data?

Possible validation: review synthetic/de-identified dependency timelines with delivery leads; measure usefulness and false-alarm burden; document the sample and method. No interview results or threshold are asserted here.

Example result wording: "Candidate requirement: show an explanation and data freshness for each suggested blocker; acceptance requires owner review and user research."
