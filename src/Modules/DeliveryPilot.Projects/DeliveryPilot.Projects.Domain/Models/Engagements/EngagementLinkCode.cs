namespace DeliveryPilot.Projects.Domain.Models.Engagements;

public sealed class EngagementLinkCode
{
    public const string OutstandingStatus = "Outstanding";
    public const string ConsumedStatus = "Consumed";
    public const string InvalidatedStatus = "Invalidated";

    private byte[] _codeVerifier = [];

    private EngagementLinkCode() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid EngagementId { get; private set; }
    public byte[] CodeVerifier => (byte[])_codeVerifier.Clone();
    public Guid IssuedByUserId { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ConsumedByUserId { get; private set; }
    public Guid? ConsumedForOrganizationId { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }
    public long Version { get; private set; }

    public bool Invalidate(DateTimeOffset occurredAt)
    {
        if (Status != OutstandingStatus)
        {
            return false;
        }

        Status = InvalidatedStatus;
        InvalidatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
        return true;
    }

    public static EngagementLinkCode Create(Guid organizationId, Guid engagementId, Guid issuedByUserId, DateTimeOffset issuedAt, byte[] codeVerifier)
    {
        ArgumentNullException.ThrowIfNull(codeVerifier);
        if (codeVerifier.Length != 32)
        {
            throw new ArgumentException("A 32-byte SHA-256 verifier is required.", nameof(codeVerifier));
        }

        DateTimeOffset instant = issuedAt.ToUniversalTime();
        return new EngagementLinkCode
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = EngagementFields.NonEmptyId(organizationId, nameof(organizationId)),
            EngagementId = EngagementFields.NonEmptyId(engagementId, nameof(engagementId)),
            IssuedByUserId = EngagementFields.NonEmptyId(issuedByUserId, nameof(issuedByUserId)),
            _codeVerifier = (byte[])codeVerifier.Clone(),
            IssuedAt = instant,
            ExpiresAt = instant.AddDays(7),
            Status = OutstandingStatus,
            Version = 1,
        };
    }
}
