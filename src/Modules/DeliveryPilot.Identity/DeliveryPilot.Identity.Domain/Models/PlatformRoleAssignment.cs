namespace DeliveryPilot.Identity.Domain.Models;

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RoleCode { get; private set; } = null!;
    public long GrantVersion { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string AuthorizationReference { get; private set; } = null!;
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string GrantReason { get; private set; } = null!;
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static PlatformRoleAssignment Create(Guid userId, string roleCode, Guid? grantedByUserId, Guid? approvedByUserId, string authorizationReference, DateTimeOffset grantedAt, string grantReason, DateTimeOffset? expiresAt = null)
    {
        if (userId == Guid.Empty || grantedByUserId == Guid.Empty || approvedByUserId == Guid.Empty)
        {
            throw new ArgumentException("User IDs must not be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReason);
        roleCode = roleCode.Trim();
        if (roleCode.Length > 80)
        {
            throw new ArgumentException("Role code must not exceed 80 characters.", nameof(roleCode));
        }

        DateTimeOffset instant = grantedAt.ToUniversalTime();
        DateTimeOffset? expiry = expiresAt?.ToUniversalTime();
        if (expiry <= instant)
        {
            throw new ArgumentException("Expiry must be after the grant.", nameof(expiresAt));
        }

        return new PlatformRoleAssignment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RoleCode = roleCode,
            GrantVersion = 1,
            GrantedByUserId = grantedByUserId,
            ApprovedByUserId = approvedByUserId,
            AuthorizationReference = authorizationReference.Trim(),
            GrantedAt = instant,
            ExpiresAt = expiry,
            GrantReason = grantReason.Trim(),
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }
}
