using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.GetEngagement;

public sealed record GetEngagementQuery(Guid OrganizationId, Guid EngagementId) : IQuery<EngagementDetail>;
