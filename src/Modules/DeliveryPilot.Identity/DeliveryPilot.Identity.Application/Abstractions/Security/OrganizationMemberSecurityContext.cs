namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed record OrganizationMemberSecurityContext(
    Guid OrganizationId,
    long OrganizationVersion,
    Guid MembershipId,
    Guid UserId,
    string UserStatus,
    string MembershipStatus,
    DateTimeOffset JoinedAt,
    long MembershipVersion,
    bool IsSystemOwner,
    bool IsSystemAdmin,
    IReadOnlyList<OrganizationRoleBrief> EffectiveRoles,
    IReadOnlySet<string> EffectivePermissions)
{
    public bool IsEligibleActiveMember =>
        string.Equals(UserStatus, "active", StringComparison.Ordinal)
        && string.Equals(MembershipStatus, "active", StringComparison.Ordinal);

    public bool HasPermission(string permissionCode) =>
        !string.IsNullOrWhiteSpace(permissionCode)
        && IsEligibleActiveMember
        && EffectivePermissions.Contains(permissionCode);
}
