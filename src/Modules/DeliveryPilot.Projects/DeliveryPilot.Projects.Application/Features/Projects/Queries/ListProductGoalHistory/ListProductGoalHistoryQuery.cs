using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProductGoalHistory;

public sealed record ListProductGoalHistoryQuery(Guid OrganizationId, Guid ProjectId) : IQuery<IReadOnlyList<ProductGoalHistoryEntry>>;
