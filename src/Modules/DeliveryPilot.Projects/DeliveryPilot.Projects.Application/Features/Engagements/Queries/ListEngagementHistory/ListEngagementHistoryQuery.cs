using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagementHistory;

public sealed record ListEngagementHistoryQuery(Guid OrganizationId, Guid EngagementId, int Limit, string? Cursor) : IQuery<EngagementPage<EngagementHistoryEntry>>;
