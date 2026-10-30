namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed record AuditPageKey(Guid Id, DateTimeOffset? OccurredAt = null, long? MembershipVersion = null, int? EventOrdinal = null);
