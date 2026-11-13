using System.Text.Json.Serialization;
using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.API.Contracts.Projects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateProjectBriefRequest(long? ExpectedVersion, string? Problem, string? ObjectivesAndSuccessCriteria, string? ScopeAndExclusions, string? Assumptions, string? ConstraintsAndDependencies, string? OpenQuestions, string? ProductGoal)
{
    public ProjectBriefValues ToValues() => new(Problem, ObjectivesAndSuccessCriteria, ScopeAndExclusions, Assumptions, ConstraintsAndDependencies, OpenQuestions, ProductGoal);
    public override string ToString() => nameof(UpdateProjectBriefRequest);
}
