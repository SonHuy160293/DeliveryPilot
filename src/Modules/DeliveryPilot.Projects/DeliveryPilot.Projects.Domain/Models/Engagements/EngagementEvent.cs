using System.Text.Json;

namespace DeliveryPilot.Projects.Domain.Models.Engagements;

/// <summary>Append-only attribution. Event-specific payloads are defined by their owning use cases.</summary>
public sealed class EngagementEvent
{
    private EngagementEvent() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EngagementId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string SubjectType { get; private set; } = null!;
    public Guid SubjectId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid ActorOrganizationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Reason { get; private set; }
    public string Details { get; private set; } = null!;
    public long EngagementVersion { get; private set; }

    public static EngagementEvent Create(Guid organizationId, Guid engagementId, string eventType, string subjectType, Guid subjectId, Guid actorUserId, Guid actorOrganizationId, DateTimeOffset occurredAt, long engagementVersion, string details, string? reason = null)
    {
        if (subjectType is not ("Engagement" or "Milestone" or "Contact" or "LinkCode"))
        {
            throw new ArgumentException("A valid event subject type is required.", nameof(subjectType));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(engagementVersion);
        using JsonDocument document = JsonDocument.Parse(details);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Event details must be a JSON object.", nameof(details));
        }

        return new EngagementEvent
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = EngagementFields.NonEmptyId(organizationId, nameof(organizationId)),
            EngagementId = EngagementFields.NonEmptyId(engagementId, nameof(engagementId)),
            EventType = EngagementFields.RequiredText(eventType, 160, nameof(eventType)),
            SubjectType = subjectType,
            SubjectId = EngagementFields.NonEmptyId(subjectId, nameof(subjectId)),
            ActorUserId = EngagementFields.NonEmptyId(actorUserId, nameof(actorUserId)),
            ActorOrganizationId = EngagementFields.NonEmptyId(actorOrganizationId, nameof(actorOrganizationId)),
            OccurredAt = occurredAt.ToUniversalTime(),
            EngagementVersion = engagementVersion,
            Details = document.RootElement.GetRawText(),
            Reason = EngagementFields.OptionalText(reason, 2000, nameof(reason)),
        };
    }
}
