using System.Collections.Frozen;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

/// <summary>FR-DP-004-005 role data. The access evaluator applies participant eligibility and guest exclusions.</summary>
public static class ProjectPermissions
{
    public const string ReadProject = "project.read";
    public const string EditBrief = "project.brief.edit";
    public const string EditStakeholders = "project.stakeholders.edit";
    public const string EditCadence = "project.cadence.edit";
    public const string ManageParticipants = "project.participants.manage";
    public const string ManageGuests = "project.guests.manage";
    public const string AssignClientPo = "project.client-po.assign";
    public const string InitializeProject = "project.initialize";
    public const string ManageAdministrators = "project.administrators.manage";
    public const string ReadHistory = "project.history.read";

    public static IReadOnlySet<string> MemberOnly { get; } = new[]
    {
        EditCadence, ManageParticipants, ManageGuests, ReadHistory,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ByRole { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [ProjectRoles.ProjectAdministrator] = new[]
            {
                ReadProject, EditBrief, EditStakeholders, EditCadence, ManageParticipants, ManageGuests,
                AssignClientPo, InitializeProject, ManageAdministrators, ReadHistory,
            }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.ProjectManager] = new[]
            {
                ReadProject, EditBrief, EditStakeholders, EditCadence, ManageParticipants, ManageGuests, ReadHistory,
            }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.BusinessAnalyst] = new[] { ReadProject, EditBrief }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.Developer] = new[] { ReadProject }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.QualityAssurance] = new[] { ReadProject }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.DevOps] = new[] { ReadProject }.ToFrozenSet(StringComparer.Ordinal),
            [ProjectRoles.Viewer] = new[] { ReadProject }.ToFrozenSet(StringComparer.Ordinal),
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
