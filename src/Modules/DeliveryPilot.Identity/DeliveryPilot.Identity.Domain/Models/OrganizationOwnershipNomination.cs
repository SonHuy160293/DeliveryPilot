namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationOwnershipNomination
{
    private OrganizationOwnershipNomination() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid NominatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? Reason { get; private set; }
    public long Version { get; private set; }

    public static OrganizationOwnershipNomination Create(Guid organizationId, Guid membershipId, Guid sponsorId, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || membershipId == Guid.Empty || sponsorId == Guid.Empty)
        {
            throw new ArgumentException("Workspace, membership and sponsor identifiers are required.");
        }

        DateTimeOffset instant = now.ToUniversalTime();
        return new OrganizationOwnershipNomination
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            MembershipId = membershipId,
            NominatedByUserId = sponsorId,
            CreatedAt = instant,
            UpdatedAt = instant,
            ExpiresAt = instant.AddDays(7),
            Status = "pending",
            Version = 1,
        };
    }

    public void Close(string status, Guid? actorId, DateTimeOffset now, string? reason = null)
    {
        DateTimeOffset instant = now.ToUniversalTime();
        string? safeReason = Auditing.IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(reason, false);
        if (Status != "pending" || status is not "accepted" and not "declined" and not "cancelled" and not "expired" ||
            instant < UpdatedAt || actorId == Guid.Empty ||
            (status == "expired" ? actorId is not null || instant < ExpiresAt : actorId is null || instant >= ExpiresAt) ||
            (status != "cancelled" && safeReason is not null))
        {
            throw new InvalidOperationException("The ownership nomination cannot make this transition.");
        }

        long nextVersion = checked(Version + 1);
        Status = status;
        ClosedAt = instant;
        ClosedByUserId = actorId;
        UpdatedAt = instant;
        Reason = safeReason;
        Version = nextVersion;
    }
}
