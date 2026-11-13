using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands;

public sealed record ProjectSettingsResult(Guid Id, string Kind, Guid? EngagementId, short SprintLengthWeeks, long Version)
{
    public static ProjectSettingsResult From(Project project) => new(project.Id, project.Kind, project.EngagementId, project.SprintLengthWeeks, project.Version);
}
