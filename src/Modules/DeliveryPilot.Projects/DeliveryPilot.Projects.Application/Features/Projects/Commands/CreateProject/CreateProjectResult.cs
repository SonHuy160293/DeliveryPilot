namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public sealed record CreateProjectResult(ProjectCreationSnapshot Snapshot, bool IsReplay);
