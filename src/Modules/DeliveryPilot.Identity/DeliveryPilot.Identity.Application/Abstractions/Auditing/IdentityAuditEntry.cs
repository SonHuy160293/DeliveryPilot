using System.Text.Json;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public sealed record IdentityAuditEntry(
    Guid? OrganizationId,
    Guid? MembershipId,
    long? MembershipVersion,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    string EventType,
    IdentityAuditActor Actor,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string DetailsJson,
    string? Reason = null,
    int? EventOrdinal = null,
    bool RequiresReason = false)
{
    public static IdentityAuditEntry ForOwnershipNomination(
        OrganizationOwnershipNomination nomination,
        string? previousStatus,
        IdentityAuditActor actor,
        DateTimeOffset now,
        string correlationId,
        long? membershipVersion = null)
    {
        ArgumentNullException.ThrowIfNull(nomination);
        string details = JsonSerializer.Serialize(new
        {
            nominatedByUserId = nomination.NominatedByUserId,
            previousStatus,
            newStatus = nomination.Status,
            expiresAt = nomination.ExpiresAt,
            closedByUserId = nomination.ClosedByUserId,
            closedAt = nomination.ClosedAt,
        });
        return new IdentityAuditEntry(
            nomination.OrganizationId,
            membershipVersion.HasValue ? nomination.MembershipId : null,
            membershipVersion,
            IdentityAuditSchemaCatalog.OrganizationOwnershipNominationAggregate,
            nomination.Id,
            nomination.Version,
            previousStatus is null ? IdentityAuditSchemaCatalog.OwnershipNominationCreated : IdentityAuditSchemaCatalog.OwnershipNominationClosed,
            actor,
            now,
            correlationId,
            details,
            nomination.Reason);
    }

    public static IdentityAuditEntry ForOrganizationCreated(
        Organization organization,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(organization);
        string details = JsonSerializer.Serialize(new
        {
            name = organization.Name,
            status = organization.Status,
            createdByUserId = organization.CreatedByUserId,
        });
        return new IdentityAuditEntry(
            organization.Id,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationAggregate,
            organization.Id,
            organization.Version,
            IdentityAuditSchemaCatalog.OrganizationCreated,
            actor,
            occurredAt,
            correlationId,
            details);
    }

    public static IdentityAuditEntry ForMembershipCreated(
        OrganizationMembership membership,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(membership);
        string details = JsonSerializer.Serialize(new
        {
            userId = membership.UserId,
            previousStatus = (string?)null,
            newStatus = membership.Status,
        });
        return new IdentityAuditEntry(
            membership.OrganizationId,
            membership.Id,
            membership.Version,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            membership.Id,
            membership.Version,
            IdentityAuditSchemaCatalog.MembershipCreated,
            actor,
            occurredAt,
            correlationId,
            details,
            reason);
    }

    public static IdentityAuditEntry ForMembershipStatusChanged(
        OrganizationMembershipAuditSnapshot before,
        OrganizationMembership after,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.MembershipId != after.Id || before.OrganizationId != after.OrganizationId || before.Version >= after.Version)
        {
            throw new ArgumentException("Before/after membership snapshots do not represent a valid committed revision.");
        }

        string details = JsonSerializer.Serialize(new
        {
            userId = after.UserId,
            previousStatus = before.Status,
            newStatus = after.Status,
        });
        bool requiresReason = string.Equals(after.Status, "removed", StringComparison.Ordinal);
        return new IdentityAuditEntry(
            after.OrganizationId,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.OrganizationMembershipAggregate,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            actor,
            occurredAt,
            correlationId,
            details,
            reason,
            RequiresReason: requiresReason);
    }

    public static IdentityAuditEntry ForRoleAssignmentGranted(
        OrganizationRoleAssignment assignment,
        long membershipVersion,
        long rolePermissionSetVersion,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        string details = JsonSerializer.Serialize(new
        {
            roleId = assignment.RoleId,
            grantVersion = assignment.GrantVersion,
            grantedByUserId = assignment.GrantedByUserId,
            grantedAt = assignment.GrantedAt.ToUniversalTime(),
            expiresAt = assignment.ExpiresAt?.ToUniversalTime(),
            permissionSetVersion = rolePermissionSetVersion,
        });
        return new IdentityAuditEntry(
            assignment.OrganizationId,
            assignment.MembershipId,
            membershipVersion,
            IdentityAuditSchemaCatalog.OrganizationRoleAssignmentAggregate,
            assignment.Id,
            assignment.Version,
            IdentityAuditSchemaCatalog.RoleAssignmentGranted,
            actor,
            occurredAt,
            correlationId,
            details,
            reason);
    }

    public static IdentityAuditEntry ForRoleAssignmentRevoked(
        OrganizationRoleAssignmentAuditSnapshot before,
        OrganizationRoleAssignment after,
        long membershipVersion,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null,
        bool isProtectedRoleReduction = false)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.AssignmentId != after.Id || before.OrganizationId != after.OrganizationId ||
            before.MembershipId != after.MembershipId || before.RevokedAt is not null ||
            after.RevokedAt is null || before.Version >= after.Version)
        {
            throw new ArgumentException("Before/after assignment snapshots do not represent a valid revocation.");
        }

        string details = JsonSerializer.Serialize(new
        {
            roleId = after.RoleId,
            grantVersion = before.GrantVersion,
            grantedAt = before.GrantedAt.ToUniversalTime(),
            expiresAt = before.ExpiresAt?.ToUniversalTime(),
            revokedAt = after.RevokedAt.Value.ToUniversalTime(),
        });
        return new IdentityAuditEntry(
            after.OrganizationId,
            after.MembershipId,
            membershipVersion,
            IdentityAuditSchemaCatalog.OrganizationRoleAssignmentAggregate,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.RoleAssignmentRevoked,
            actor,
            occurredAt,
            correlationId,
            details,
            reason,
            RequiresReason: isProtectedRoleReduction);
    }

    public static IdentityAuditEntry ForRoleAssignmentRegranted(
        OrganizationRoleAssignmentAuditSnapshot before,
        OrganizationRoleAssignment after,
        long membershipVersion,
        long rolePermissionSetVersion,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.AssignmentId != after.Id || before.OrganizationId != after.OrganizationId ||
            before.MembershipId != after.MembershipId || after.RevokedAt is not null ||
            after.GrantVersion <= before.GrantVersion || before.Version >= after.Version)
        {
            throw new ArgumentException("Before/after assignment snapshots do not represent a valid regrant.");
        }

        string details = JsonSerializer.Serialize(new
        {
            roleId = after.RoleId,
            previousGrantVersion = before.GrantVersion,
            grantVersion = after.GrantVersion,
            grantedByUserId = after.GrantedByUserId,
            grantedAt = after.GrantedAt.ToUniversalTime(),
            expiresAt = after.ExpiresAt?.ToUniversalTime(),
            permissionSetVersion = rolePermissionSetVersion,
        });
        return new IdentityAuditEntry(
            after.OrganizationId,
            after.MembershipId,
            membershipVersion,
            IdentityAuditSchemaCatalog.OrganizationRoleAssignmentAggregate,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.RoleAssignmentRegranted,
            actor,
            occurredAt,
            correlationId,
            details,
            reason);
    }

    public static IdentityAuditEntry ForRoleCreated(
        OrganizationRole role,
        IEnumerable<string> permissionCodes,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(permissionCodes);
        string[] codes = permissionCodes.Select(code => code.Trim()).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        string details = JsonSerializer.Serialize(new
        {
            origin = role.Origin,
            templateVersion = role.TemplateVersion,
            previousPermissionSetVersion = (long?)null,
            permissionSetVersion = role.PermissionSetVersion,
            addedPermissionCodes = codes,
            removedPermissionCodes = Array.Empty<string>(),
            permissionCodes = codes,
        });
        return new IdentityAuditEntry(
            role.OrganizationId,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationRoleAggregate,
            role.Id,
            role.Version,
            IdentityAuditSchemaCatalog.RoleCreated,
            actor,
            occurredAt,
            correlationId,
            details);
    }

    public static IdentityAuditEntry ForRoleUpdated(
        OrganizationRoleAuditSnapshot before,
        OrganizationRole after,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.RoleId != after.Id || before.OrganizationId != after.OrganizationId || before.Version >= after.Version
            || (before.Name == after.Name && before.Description == after.Description))
        {
            throw new ArgumentException("Before/after role snapshots do not represent a valid metadata change.");
        }

        string details = JsonSerializer.Serialize(new
        {
            previousName = before.Name,
            newName = after.Name,
            descriptionChanged = before.Description != after.Description,
        });
        return new IdentityAuditEntry(
            after.OrganizationId,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationRoleAggregate,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.RoleUpdated,
            actor,
            occurredAt,
            correlationId,
            details);
    }

    public static IdentityAuditEntry ForRolePermissionsChanged(
        OrganizationRoleAuditSnapshot before,
        OrganizationRole after,
        IEnumerable<string> currentPermissionCodes,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(currentPermissionCodes);
        if (before.RoleId != after.Id || before.OrganizationId != after.OrganizationId ||
            after.PermissionSetVersion <= before.PermissionSetVersion || before.Version >= after.Version)
        {
            throw new ArgumentException("Before/after role snapshots do not represent a valid permission-set change.");
        }

        string[] current = currentPermissionCodes.Select(code => code.Trim()).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        string[] added = current.Except(before.PermissionCodes, StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        string[] removed = before.PermissionCodes.Except(current, StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();

        string details = JsonSerializer.Serialize(new
        {
            origin = after.Origin,
            templateVersion = after.TemplateVersion,
            previousPermissionSetVersion = before.PermissionSetVersion,
            permissionSetVersion = after.PermissionSetVersion,
            addedPermissionCodes = added,
            removedPermissionCodes = removed,
            permissionCodes = current,
        });
        return new IdentityAuditEntry(
            after.OrganizationId,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationRoleAggregate,
            after.Id,
            after.Version,
            IdentityAuditSchemaCatalog.RolePermissionsChanged,
            actor,
            occurredAt,
            correlationId,
            details);
    }

    public static IdentityAuditEntry ForRoleRetired(
        OrganizationRole role,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.RetiredAt is null)
        {
            throw new ArgumentException("Role must be retired before staging a retirement audit event.", nameof(role));
        }

        string details = JsonSerializer.Serialize(new
        {
            code = role.Code,
            name = role.Name,
            retiredAt = role.RetiredAt.Value.ToUniversalTime(),
            permissionSetVersion = role.PermissionSetVersion,
        });
        return new IdentityAuditEntry(
            role.OrganizationId,
            null,
            null,
            IdentityAuditSchemaCatalog.OrganizationRoleAggregate,
            role.Id,
            role.Version,
            IdentityAuditSchemaCatalog.RoleRetired,
            actor,
            occurredAt,
            correlationId,
            details,
            reason,
            RequiresReason: true);
    }

    public static IdentityAuditEntry ForInvitationClosed(
        Guid organizationId,
        Guid invitationId,
        long invitationVersion,
        string previousStatus,
        string newStatus,
        Guid? closedByUserId,
        DateTimeOffset closedAt,
        IdentityAuditActor actor,
        DateTimeOffset occurredAt,
        string correlationId,
        string? reason = null,
        Guid? membershipId = null,
        long? membershipVersion = null)
    {
        string details = JsonSerializer.Serialize(new
        {
            previousStatus,
            newStatus,
            closedByUserId,
            closedAt = closedAt.ToUniversalTime(),
        });
        bool requiresReason = string.Equals(newStatus, "cancelled", StringComparison.Ordinal);
        return new IdentityAuditEntry(
            organizationId,
            membershipId,
            membershipVersion,
            IdentityAuditSchemaCatalog.OrganizationInvitationAggregate,
            invitationId,
            invitationVersion,
            IdentityAuditSchemaCatalog.InvitationClosed,
            actor,
            occurredAt,
            correlationId,
            details,
            reason,
            RequiresReason: requiresReason);
    }
}
