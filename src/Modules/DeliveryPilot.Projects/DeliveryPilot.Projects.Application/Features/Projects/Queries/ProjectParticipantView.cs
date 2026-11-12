namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectParticipantView(Guid UserId, string Kind, IReadOnlyList<string> Roles, string? DisplayName);
