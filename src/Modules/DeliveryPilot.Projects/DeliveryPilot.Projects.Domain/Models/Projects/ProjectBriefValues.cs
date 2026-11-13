namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed record ProjectBriefValues(string? Problem, string? ObjectivesAndSuccessCriteria, string? ScopeAndExclusions, string? Assumptions, string? ConstraintsAndDependencies, string? OpenQuestions, string? ProductGoal)
{
    public ProjectBriefValues Normalize() => new(
        ProjectFields.OptionalText(Problem, 4000, nameof(Problem)),
        ProjectFields.OptionalText(ObjectivesAndSuccessCriteria, 4000, nameof(ObjectivesAndSuccessCriteria)),
        ProjectFields.OptionalText(ScopeAndExclusions, 4000, nameof(ScopeAndExclusions)),
        ProjectFields.OptionalText(Assumptions, 4000, nameof(Assumptions)),
        ProjectFields.OptionalText(ConstraintsAndDependencies, 4000, nameof(ConstraintsAndDependencies)),
        ProjectFields.OptionalText(OpenQuestions, 4000, nameof(OpenQuestions)),
        ProjectFields.OptionalText(ProductGoal, 4000, nameof(ProductGoal)));

    public override string ToString() => nameof(ProjectBriefValues);
}
