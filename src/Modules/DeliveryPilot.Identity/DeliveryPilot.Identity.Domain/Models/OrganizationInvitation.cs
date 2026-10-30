namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationInvitation
{
    private byte[] _tokenVerifier = null!;

    private OrganizationInvitation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string RecipientEmail { get; private set; } = null!;
    public Guid IssuedByUserId { get; private set; }
    public DateTimeOffset LastIssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public byte[] TokenVerifier { get => (byte[])_tokenVerifier.Clone(); private set => _tokenVerifier = (byte[])value.Clone(); }
    public string Status { get; private set; } = null!;
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? CloseReason { get; private set; }
    public Guid? ReplacesInvitationId { get; private set; }
    public Guid? CurrentDeliveryMessageId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static OrganizationInvitation Create(Guid organizationId, string recipientEmail, Guid issuedByUserId, DateTimeOffset issuedAt, DateTimeOffset expiresAt, byte[] tokenVerifier, Guid? replacesInvitationId = null)
    {
        if (organizationId == Guid.Empty || issuedByUserId == Guid.Empty || replacesInvitationId == Guid.Empty)
        {
            throw new ArgumentException("IDs must not be empty.");
        }

        ArgumentNullException.ThrowIfNull(tokenVerifier);
        if (tokenVerifier.Length != 32)
        {
            throw new ArgumentException("Token verifier must contain 32 bytes.", nameof(tokenVerifier));
        }

        DateTimeOffset issued = issuedAt.ToUniversalTime();
        DateTimeOffset expiry = expiresAt.ToUniversalTime();
        if (expiry <= issued)
        {
            throw new ArgumentException("Expiry must be after issuance.", nameof(expiresAt));
        }

        return new OrganizationInvitation
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            RecipientEmail = User.NormalizeEmail(recipientEmail),
            IssuedByUserId = issuedByUserId,
            LastIssuedAt = issued,
            ExpiresAt = expiry,
            TokenVerifier = tokenVerifier,
            Status = "pending",
            ReplacesInvitationId = replacesInvitationId,
            CreatedAt = issued,
            UpdatedAt = issued,
            Version = 1,
        };
    }

    public static OrganizationInvitation CreateWithId(Guid id, Guid organizationId, string recipientEmail, Guid issuedByUserId, DateTimeOffset issuedAt, DateTimeOffset expiresAt, byte[] tokenVerifier, Guid deliveryMessageId, Guid? replacesInvitationId = null)
    {
        if (id == Guid.Empty || deliveryMessageId == Guid.Empty)
        {
            throw new ArgumentException("Invitation and delivery identifiers are required.");
        }

        OrganizationInvitation invitation = Create(organizationId, recipientEmail, issuedByUserId, issuedAt, expiresAt, tokenVerifier, replacesInvitationId);
        invitation.Id = id;
        invitation.CurrentDeliveryMessageId = deliveryMessageId;
        return invitation;
    }

    public void RotateProof(Guid deliveryMessageId, byte[] verifier, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        DateTimeOffset issued = issuedAt.ToUniversalTime();
        DateTimeOffset expiry = expiresAt.ToUniversalTime();
        if (Status != "pending" || issued < UpdatedAt || issued >= ExpiresAt || expiry <= issued || deliveryMessageId == Guid.Empty || verifier.Length != 32)
        {
            throw new InvalidOperationException("The invitation cannot be issued with these values.");
        }

        TokenVerifier = verifier;
        CurrentDeliveryMessageId = deliveryMessageId;
        LastIssuedAt = issued;
        ExpiresAt = expiry;
        UpdatedAt = issued;
        Version = checked(Version + 1);
    }

    public void Close(string status, Guid? actorUserId, DateTimeOffset occurredAt, string? reason = null)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (Status != "pending" || instant < UpdatedAt || status is not ("accepted" or "declined" or "cancelled" or "expired")
            || actorUserId == Guid.Empty || ((status is "accepted" or "declined") && actorUserId is null)
            || (status == "expired" && (actorUserId is not null || instant < ExpiresAt))
            || ((status is "accepted" or "declined") && instant >= ExpiresAt)
            || (reason is not null && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)))
        {
            throw new InvalidOperationException("The invitation cannot be closed with these values.");
        }

        Status = status;
        ClosedAt = instant;
        ClosedByUserId = actorUserId;
        CloseReason = reason?.Trim();
        UpdatedAt = instant;
        Version = checked(Version + 1);
    }
}
