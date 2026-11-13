using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectBrief
{
    private ProjectBrief() { }

    public Guid ProjectId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string? Problem { get; private set; }
    public string? ObjectivesAndSuccessCriteria { get; private set; }
    public string? ScopeAndExclusions { get; private set; }
    public string? Assumptions { get; private set; }
    public string? ConstraintsAndDependencies { get; private set; }
    public string? OpenQuestions { get; private set; }
    public string? ProductGoal { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public long Version { get; private set; }

    public static ProjectBrief CreateFor(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectBrief { ProjectId = project.Id, OrganizationId = project.OrganizationId, Version = 1 };
    }

    public IReadOnlyDictionary<string, ProjectBriefChange> Update(ProjectBriefValues values, long expectedVersion, Guid actor, DateTimeOffset occurredAt)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("ProjectBrief.VersionConflict", "The project brief has changed.");
        }

        ProjectBriefValues normalized = values.Normalize();
        var changes = new Dictionary<string, ProjectBriefChange>(StringComparer.Ordinal);
        Add("problem", Problem, normalized.Problem);
        Add("objectivesAndSuccessCriteria", ObjectivesAndSuccessCriteria, normalized.ObjectivesAndSuccessCriteria);
        Add("scopeAndExclusions", ScopeAndExclusions, normalized.ScopeAndExclusions);
        Add("assumptions", Assumptions, normalized.Assumptions);
        Add("constraintsAndDependencies", ConstraintsAndDependencies, normalized.ConstraintsAndDependencies);
        Add("openQuestions", OpenQuestions, normalized.OpenQuestions);
        Add("productGoal", ProductGoal, normalized.ProductGoal);
        if (changes.Count == 0)
        {
            return changes;
        }

        Problem = normalized.Problem;
        ObjectivesAndSuccessCriteria = normalized.ObjectivesAndSuccessCriteria;
        ScopeAndExclusions = normalized.ScopeAndExclusions;
        Assumptions = normalized.Assumptions;
        ConstraintsAndDependencies = normalized.ConstraintsAndDependencies;
        OpenQuestions = normalized.OpenQuestions;
        ProductGoal = normalized.ProductGoal;
        UpdatedAt = occurredAt.ToUniversalTime();
        UpdatedByUserId = ProjectFields.NonEmptyId(actor, nameof(actor));
        Version = checked(Version + 1);
        return changes;

        void Add(string name, string? previous, string? next)
        {
            if (!string.Equals(previous, next, StringComparison.Ordinal))
            {
                changes.Add(name, new ProjectBriefChange(previous, next));
            }
        }
    }
}
