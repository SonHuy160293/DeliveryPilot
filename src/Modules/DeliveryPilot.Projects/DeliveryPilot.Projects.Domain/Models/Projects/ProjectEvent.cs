using System.Text.Json;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

/// <summary>Append-only attribution. Payloads are defined by the use case that writes each event.</summary>
public sealed class ProjectEvent
{
    private ProjectEvent() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string SubjectType { get; private set; } = null!;
    public Guid SubjectId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid ActorOrganizationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public long ProjectVersion { get; private set; }
    public string? Reason { get; private set; }
    public string Details { get; private set; } = null!;

    public static ProjectEvent Create(Guid organizationId, Guid projectId, string eventType, string subjectType, Guid subjectId, Guid actorUserId, Guid actorOrganizationId, DateTimeOffset occurredAt, long projectVersion, string details, string? reason = null)
    {
        if (subjectType is not ("Project" or "Brief" or "Participant" or "RoleGrant" or "Invitation" or "Stakeholder" or "ClientPo" or "GuestShare"))
        {
            throw new ArgumentException("A valid event subject type is required.", nameof(subjectType));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(projectVersion);
        using JsonDocument document = JsonDocument.Parse(details);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Event details must be a JSON object.", nameof(details));
        }

        return new ProjectEvent
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            ProjectId = ProjectFields.NonEmptyId(projectId, nameof(projectId)),
            EventType = ProjectFields.RequiredText(eventType, 160, nameof(eventType)),
            SubjectType = subjectType,
            SubjectId = ProjectFields.NonEmptyId(subjectId, nameof(subjectId)),
            ActorUserId = ProjectFields.NonEmptyId(actorUserId, nameof(actorUserId)),
            ActorOrganizationId = ProjectFields.NonEmptyId(actorOrganizationId, nameof(actorOrganizationId)),
            OccurredAt = occurredAt.ToUniversalTime(),
            ProjectVersion = projectVersion,
            Reason = ProjectFields.OptionalText(reason, 2000, nameof(reason)),
            Details = document.RootElement.GetRawText(),
        };
    }
}
