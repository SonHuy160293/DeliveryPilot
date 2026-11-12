using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectGuestInvitation
{
    public const string PendingStatus = "Pending";
    public const string AcceptedStatus = "Accepted";
    public const string DeclinedStatus = "Declined";
    public const string CancelledStatus = "Cancelled";
    public const string ExpiredStatus = "Expired";

    private byte[] _tokenVerifier = [];

    private ProjectGuestInvitation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string RecipientEmail { get; private set; } = null!;
    public string OfferedRoleCode { get; private set; } = null!;
    public byte[] TokenVerifier => (byte[])_tokenVerifier.Clone();
    public string Status { get; private set; } = null!;
    public Guid IssuedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastIssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? CurrentDeliveryMessageId { get; private set; }
    public Guid? ReplacesInvitationId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public long Version { get; private set; }

    public static ProjectGuestInvitation Issue(Guid organizationId, Guid projectId, Guid issuedByUserId, DateTimeOffset issuedAt, string recipientEmail, string offeredRoleCode, byte[] tokenVerifier, Guid? replacesInvitationId = null)
    {
        ArgumentNullException.ThrowIfNull(tokenVerifier);
        if (tokenVerifier.Length != 32)
        {
            throw new BadRequestException("Project.InvalidVerifier", "A 32-byte invitation verifier is required.");
        }

        DateTimeOffset instant = issuedAt.ToUniversalTime();
        return new ProjectGuestInvitation
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            ProjectId = ProjectFields.NonEmptyId(projectId, nameof(projectId)),
            IssuedByUserId = ProjectFields.NonEmptyId(issuedByUserId, nameof(issuedByUserId)),
            RecipientEmail = ProjectFields.Email(recipientEmail),
            OfferedRoleCode = ProjectFields.Role(offeredRoleCode, guest: true),
            _tokenVerifier = (byte[])tokenVerifier.Clone(),
            Status = PendingStatus,
            CreatedAt = instant,
            LastIssuedAt = instant,
            ExpiresAt = instant.AddDays(7),
            ReplacesInvitationId = ProjectFields.OptionalId(replacesInvitationId, nameof(replacesInvitationId)),
            Version = 1,
        };
    }
}
