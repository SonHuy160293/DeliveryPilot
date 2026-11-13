namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProductGoalHistoryEntry(Guid Id, Guid ActorUserId, DateTimeOffset OccurredAt, string? Previous, string? New);
