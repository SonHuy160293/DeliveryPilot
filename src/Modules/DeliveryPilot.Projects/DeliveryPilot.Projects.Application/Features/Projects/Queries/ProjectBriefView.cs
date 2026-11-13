using System.Text.Json.Serialization;
using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectBriefView(string? ProductGoal, IReadOnlyList<string> MissingItems, long Version)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Problem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ObjectivesAndSuccessCriteria { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ScopeAndExclusions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Assumptions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ConstraintsAndDependencies { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OpenQuestions { get; init; }

    public static ProjectBriefView From(ProjectBrief brief, bool full)
    {
        var missing = new List<string>();
        if (full)
        {
            Add("problem", brief.Problem);
            Add("objectivesAndSuccessCriteria", brief.ObjectivesAndSuccessCriteria);
            Add("scopeAndExclusions", brief.ScopeAndExclusions);
            Add("assumptions", brief.Assumptions);
            Add("constraintsAndDependencies", brief.ConstraintsAndDependencies);
            Add("openQuestions", brief.OpenQuestions);
        }

        Add("productGoal", brief.ProductGoal);
        return new ProjectBriefView(brief.ProductGoal, missing, brief.Version)
        {
            Problem = full ? brief.Problem : null,
            ObjectivesAndSuccessCriteria = full ? brief.ObjectivesAndSuccessCriteria : null,
            ScopeAndExclusions = full ? brief.ScopeAndExclusions : null,
            Assumptions = full ? brief.Assumptions : null,
            ConstraintsAndDependencies = full ? brief.ConstraintsAndDependencies : null,
            OpenQuestions = full ? brief.OpenQuestions : null,
        };

        void Add(string name, string? value)
        {
            if (value is null)
            {
                missing.Add(name);
            }
        }
    }
}
