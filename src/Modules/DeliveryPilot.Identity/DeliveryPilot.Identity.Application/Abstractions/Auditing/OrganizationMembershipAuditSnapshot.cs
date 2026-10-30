using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public sealed record OrganizationMembershipAuditSnapshot(
    Guid MembershipId,
    Guid OrganizationId,
    Guid UserId,
    string Status,
    long Version)
{
    public static OrganizationMembershipAuditSnapshot Capture(OrganizationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return new OrganizationMembershipAuditSnapshot(
            membership.Id,
            membership.OrganizationId,
            membership.UserId,
            membership.Status,
            membership.Version);
    }
}
