using System.Text.Json;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed record OrganizationAuditEventResponse(
    Guid EventId,
    Guid? ActorUserId,
    string? ActorDisplayName,
    Guid OrganizationId,
    string TargetType,
    Guid TargetId,
    string Action,
    DateTimeOffset OccurredAt,
    string Outcome,
    long AggregateVersion,
    long? MembershipVersion,
    int EventOrdinal,
    string? Reason,
    JsonElement Changes);
