using DeliveryPilot.Identity.Domain.Auditing;

namespace DeliveryPilot.Identity.Domain.Models;

public sealed class IdentityAuditEvent
{
    private IdentityAuditEvent() { }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public long? MembershipVersion { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public int EventOrdinal { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Outcome { get; private set; } = null!;
    public string ActorKind { get; private set; } = null!;
    public Guid? ActorUserId { get; private set; }
    public string? ActorReference { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string CorrelationId { get; private set; } = null!;
    public string? Reason { get; private set; }
    public short DetailsSchemaVersion { get; private set; }
    public string Details { get; private set; } = null!;

    public static IdentityAuditEvent Create(
        Guid? organizationId,
        Guid? membershipId,
        long? membershipVersion,
        string aggregateType,
        Guid aggregateId,
        long aggregateVersion,
        int eventOrdinal,
        string eventType,
        string outcome,
        string actorKind,
        Guid? actorUserId,
        string? actorReference,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        string correlationId,
        string detailsJson,
        string? reason = null,
        short detailsSchemaVersion = 1,
        bool requiresReason = false)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId must not be empty when supplied.", nameof(organizationId));
        }

        if (!((membershipId is null && membershipVersion is null) ||
              (organizationId is not null && membershipId is not null && membershipId != Guid.Empty && membershipVersion is > 0)))
        {
            throw new ArgumentException("MembershipId and MembershipVersion must either both be null or both be valid with a non-null OrganizationId.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string normalizedAggregateType = aggregateType.Trim();
        string normalizedEventType = eventType.Trim();
        string normalizedOutcome = outcome.Trim();
        string normalizedActorKind = actorKind.Trim();
        string normalizedCorrelationId = correlationId.Trim();

        if (normalizedAggregateType.Length > 80 || normalizedEventType.Length > 160 || normalizedCorrelationId.Length > 100)
        {
            throw new ArgumentException("AggregateType, EventType, or CorrelationId exceeds its maximum length.");
        }

        if (aggregateId == Guid.Empty)
        {
            throw new ArgumentException("AggregateId must not be empty.", nameof(aggregateId));
        }

        if (aggregateVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aggregateVersion), "AggregateVersion must be positive.");
        }

        if (eventOrdinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventOrdinal), "EventOrdinal must be positive.");
        }

        if (normalizedOutcome is not ("succeeded" or "denied" or "failed"))
        {
            throw new ArgumentException("Outcome must be succeeded, denied, or failed.", nameof(outcome));
        }

        string? normalizedActorReference = ValidateActor(normalizedActorKind, actorUserId, actorReference);
        (string canonicalDetails, bool payloadRequiresReason) = IdentityAuditSchemaCatalog.ValidateAndCanonicalizeDetails(
            normalizedAggregateType,
            normalizedEventType,
            detailsSchemaVersion,
            detailsJson);
        string? normalizedReason = IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(
            reason,
            requiresReason || payloadRequiresReason);

        return new IdentityAuditEvent
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            MembershipId = membershipId,
            MembershipVersion = membershipVersion,
            AggregateType = normalizedAggregateType,
            AggregateId = aggregateId,
            AggregateVersion = aggregateVersion,
            EventOrdinal = eventOrdinal,
            EventType = normalizedEventType,
            Outcome = normalizedOutcome,
            ActorKind = normalizedActorKind,
            ActorUserId = actorUserId,
            ActorReference = normalizedActorReference,
            OccurredAt = occurredAt.ToUniversalTime(),
            RecordedAt = recordedAt.ToUniversalTime(),
            CorrelationId = normalizedCorrelationId,
            Reason = normalizedReason,
            DetailsSchemaVersion = detailsSchemaVersion,
            Details = canonicalDetails,
        };
    }

    private static string? ValidateActor(string actorKind, Guid? actorUserId, string? actorReference)
    {
        if (actorKind == "user")
        {
            if (actorUserId is null || actorUserId == Guid.Empty || actorReference is not null)
            {
                throw new ArgumentException("User actor requires a valid ActorUserId and a null ActorReference.");
            }

            return null;
        }

        if (actorKind is "system" or "operator")
        {
            if (actorUserId is not null || string.IsNullOrWhiteSpace(actorReference))
            {
                throw new ArgumentException("System or operator actor requires a nonblank ActorReference and a null ActorUserId.");
            }

            string trimmedReference = actorReference.Trim();
            if (trimmedReference.Length > 200)
            {
                throw new ArgumentException("ActorReference must not exceed 200 characters.", nameof(actorReference));
            }

            return trimmedReference;
        }

        throw new ArgumentException("ActorKind must be user, system, or operator.", nameof(actorKind));
    }
}
