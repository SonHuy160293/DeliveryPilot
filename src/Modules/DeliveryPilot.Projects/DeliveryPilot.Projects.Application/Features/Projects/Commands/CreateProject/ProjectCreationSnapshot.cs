namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public sealed record ProjectCreationSnapshot(Guid Id, string Status, string Kind, Guid? EngagementId, short SprintLengthWeeks, long Version)
{
    public override string ToString() => nameof(ProjectCreationSnapshot);
}
