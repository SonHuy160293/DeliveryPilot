using System.Collections.Frozen;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public static class ProjectRoles
{
    public const string ProjectAdministrator = "ProjectAdministrator";
    public const string ProjectManager = "ProjectManager";
    public const string BusinessAnalyst = "BusinessAnalyst";
    public const string Developer = "Developer";
    public const string QualityAssurance = "QualityAssurance";
    public const string DevOps = "DevOps";
    public const string Viewer = "Viewer";

    public static IReadOnlySet<string> All { get; } = new[]
    {
        ProjectAdministrator, ProjectManager, BusinessAnalyst, Developer, QualityAssurance, DevOps, Viewer,
    }.ToFrozenSet(StringComparer.Ordinal);
}
