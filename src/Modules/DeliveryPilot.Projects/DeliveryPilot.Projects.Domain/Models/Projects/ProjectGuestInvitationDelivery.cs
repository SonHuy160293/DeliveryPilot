using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectGuestInvitationDelivery
{
    public const string QueuedStatus = "queued";
    public const string RetryingStatus = "retrying";
    public const string ProviderAcceptedStatus = "provider-accepted";
    public const string DeliveredStatus = "delivered";
    public const string BouncedStatus = "bounced";
    public const string ComplainedStatus = "complained";
    public const string FailedStatus = "failed";
    public const string SuppressedStatus = "suppressed";
    public const string ExpiredStatus = "expired";
    public const string ObsoleteStatus = "obsolete";

    private ProjectGuestInvitationDelivery() { }

    public Guid DeliveryMessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid InvitationId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Status { get; private set; } = null!;
    public long LastEventSequence { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static ProjectGuestInvitationDelivery Create(Guid organizationId, Guid projectId, Guid invitationId, Guid deliveryMessageId, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        DateTimeOffset instant = issuedAt.ToUniversalTime();
        DateTimeOffset deadline = expiresAt.ToUniversalTime();
        if (deliveryMessageId.Version != 7 || deadline <= instant)
        {
            throw new BadRequestException("Project.InvalidDelivery", "A UUIDv7 delivery message and a future deadline are required.");
        }

        return new ProjectGuestInvitationDelivery
        {
            DeliveryMessageId = deliveryMessageId,
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            ProjectId = ProjectFields.NonEmptyId(projectId, nameof(projectId)),
            InvitationId = ProjectFields.NonEmptyId(invitationId, nameof(invitationId)),
            IssuedAt = instant,
            ExpiresAt = deadline,
            Status = QueuedStatus,
            UpdatedAt = instant,
            Version = 1,
        };
    }
}
