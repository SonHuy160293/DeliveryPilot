using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public sealed record EngagementMutationContext(Engagement Engagement, Guid UserId, DateTimeOffset OccurredAt);
