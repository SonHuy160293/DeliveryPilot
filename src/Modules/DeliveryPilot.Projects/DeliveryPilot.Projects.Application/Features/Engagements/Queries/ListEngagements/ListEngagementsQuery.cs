using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagements;

public sealed record ListEngagementsQuery(Guid OrganizationId, string? Status, int Limit, string? Cursor) : IQuery<EngagementPage<EngagementListEntry>>;
