using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public sealed record OrganizationRoleAssignmentAuditSnapshot(
    Guid AssignmentId,
    Guid OrganizationId,
    Guid MembershipId,
    Guid RoleId,
    long GrantVersion,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    long Version)
{
    public static OrganizationRoleAssignmentAuditSnapshot Capture(OrganizationRoleAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return new OrganizationRoleAssignmentAuditSnapshot(
            assignment.Id,
            assignment.OrganizationId,
            assignment.MembershipId,
            assignment.RoleId,
            assignment.GrantVersion,
            assignment.GrantedAt,
            assignment.ExpiresAt,
            assignment.RevokedAt,
            assignment.Version);
    }
}
