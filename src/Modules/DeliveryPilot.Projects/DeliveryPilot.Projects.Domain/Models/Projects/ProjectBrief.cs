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
}
