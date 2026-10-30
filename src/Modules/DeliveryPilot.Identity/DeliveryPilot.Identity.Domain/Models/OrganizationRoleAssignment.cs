namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationRoleAssignment
{
    private OrganizationRoleAssignment() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid RoleId { get; private set; }
    public long GrantVersion { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? GrantReason { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static OrganizationRoleAssignment Create(Guid organizationId, Guid membershipId, Guid roleId, Guid grantedByUserId, DateTimeOffset grantedAt, DateTimeOffset? expiresAt = null, string? grantReason = null)
    {
        if (organizationId == Guid.Empty || membershipId == Guid.Empty || roleId == Guid.Empty || grantedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Organization, membership, role and grantor IDs must not be empty.");
        }

        DateTimeOffset instant = grantedAt.ToUniversalTime();
        DateTimeOffset? expiry = expiresAt?.ToUniversalTime();
        if (expiry <= instant)
        {
            throw new ArgumentException("Expiry must be after the grant.", nameof(expiresAt));
        }

        return new OrganizationRoleAssignment
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            MembershipId = membershipId,
            RoleId = roleId,
            GrantVersion = 1,
            GrantedByUserId = grantedByUserId,
            GrantedAt = instant,
            ExpiresAt = expiry,
            GrantReason = grantReason,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    public void Revoke(Guid actorUserId, DateTimeOffset occurredAt, string? reason = null)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        if (actorUserId == Guid.Empty || instant < UpdatedAt || RevokedAt is not null)
        {
            throw new InvalidOperationException("The assignment cannot be revoked with these inputs.");
        }

        long nextVersion = checked(Version + 1);
        RevokedAt = instant;
        RevokedByUserId = actorUserId;
        RevocationReason = reason;
        UpdatedAt = instant;
        Version = nextVersion;
    }

    public void Regrant(Guid actorUserId, DateTimeOffset occurredAt, DateTimeOffset? expiresAt = null, string? reason = null)
    {
        DateTimeOffset instant = occurredAt.ToUniversalTime();
        DateTimeOffset? expiry = expiresAt?.ToUniversalTime();
        if (actorUserId == Guid.Empty || instant < UpdatedAt || expiry <= instant ||
            (RevokedAt is null && (ExpiresAt is null || ExpiresAt > instant)))
        {
            throw new InvalidOperationException("The assignment cannot be regranted with these inputs.");
        }

        long nextGrantVersion = checked(GrantVersion + 1);
        long nextVersion = checked(Version + 1);
        GrantVersion = nextGrantVersion;
        GrantedByUserId = actorUserId;
        GrantedAt = instant;
        ExpiresAt = expiry;
        GrantReason = reason;
        RevokedAt = null;
        RevokedByUserId = null;
        RevocationReason = null;
        UpdatedAt = instant;
        Version = nextVersion;
    }
}
