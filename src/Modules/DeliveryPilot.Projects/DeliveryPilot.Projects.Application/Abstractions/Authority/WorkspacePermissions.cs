namespace DeliveryPilot.Projects.Application.Abstractions.Authority;

/// <summary>
/// Workspace entry-point permission codes owned by Identity's capability catalog (TASK-DP-003-020).
/// Values must match Identity exactly; Projects does not reference Identity's Domain.
/// </summary>
public static class WorkspacePermissions
{
    public const string EngagementsCreate = "organization.engagements.create";
    public const string EngagementsReadAll = "organization.engagements.read-all";
    public const string EngagementsManage = "organization.engagements.manage";
    public const string ProjectsCreate = "organization.projects.create";
}
