namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public sealed record CreateEngagementResult(EngagementCreationSnapshot Snapshot, bool IsReplay);
