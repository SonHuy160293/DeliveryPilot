using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed record OrganizationMutationScope(
    Organization Organization,
    OrganizationMembership ActorMembership,
    OrganizationMemberSecurityContext Actor,
    int ActiveEligibleOwnerCount,
    OrganizationMembership? TargetMembership = null,
    OrganizationMemberSecurityContext? Target = null,
    OrganizationMutationScope? Sponsor = null)
{
    public const string AccessDeniedErrorCode = "Organization.AccessDenied";
    public const string AccessDeniedMessage = "Access is not permitted.";
    public const string OwnerContinuityErrorCode = "Organization.OwnerContinuityRequired";
    public const string OwnerContinuityMessage = "At least one active eligible Owner must remain in the workspace.";

    public bool HasActiveEligibleOwner => ActiveEligibleOwnerCount > 0;

    public void EnsureCapability(string requiredPermissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermissionCode);

        if (!string.Equals(Organization.Status, "active", StringComparison.Ordinal)
            || !Actor.HasPermission(requiredPermissionCode))
        {
            throw CreateAccessDenied();
        }
    }

    public void EnsureOwnerAuthority()
    {
        if (!string.Equals(Organization.Status, "active", StringComparison.Ordinal)
            || !Actor.IsEligibleActiveMember
            || !Actor.IsSystemOwner)
        {
            throw CreateAccessDenied();
        }
    }

    public void EnsurePrivilegedChangeAllowed()
    {
        if (!string.Equals(Organization.Status, "active", StringComparison.Ordinal)
            || !Actor.IsEligibleActiveMember)
        {
            throw CreateAccessDenied();
        }

        if (!HasActiveEligibleOwner)
        {
            throw new ConflictException(OwnerContinuityErrorCode, OwnerContinuityMessage);
        }
    }

    public void EnsureCanDefineCustomRole(IReadOnlyCollection<string> requestedPermissionCodes)
    {
        ArgumentNullException.ThrowIfNull(requestedPermissionCodes);
        EnsureCapability(OrganizationCapabilityCatalog.RolesManage);
        EnsurePrivilegedChangeAllowed();

        foreach (string code in requestedPermissionCodes)
        {
            if (!OrganizationCapabilityCatalog.IsDelegablePermission(code)
                || !Actor.EffectivePermissions.Contains(code))
            {
                throw CreateAccessDenied();
            }
        }
    }

    public void EnsureCanGrantOrdinaryRole(
        OrganizationRole role,
        IReadOnlyCollection<OrganizationRolePermission> rolePermissions,
        OrganizationMemberSecurityContext? targetMember = null)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(rolePermissions);

        EnsureCapability(OrganizationCapabilityCatalog.RolesAssign);
        EnsurePrivilegedChangeAllowed();

        if (role.OrganizationId != Organization.Id
            || role.RetiredAt is not null
            || OrganizationCapabilityCatalog.IsSystemOwnerRole(role))
        {
            throw CreateAccessDenied();
        }

        if (targetMember is not null)
        {
            if (targetMember.OrganizationId != Organization.Id
                || !targetMember.IsEligibleActiveMember)
            {
                throw CreateAccessDenied();
            }

            if (targetMember.IsSystemOwner && !Actor.IsSystemOwner)
            {
                throw CreateAccessDenied();
            }
        }

        IReadOnlySet<string> targetRolePermissions = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, rolePermissions);
        foreach (string code in targetRolePermissions)
        {
            if (!OrganizationCapabilityCatalog.IsDelegablePermission(code)
                || !Actor.EffectivePermissions.Contains(code))
            {
                throw CreateAccessDenied();
            }
        }
    }

    public void EnsureCanRevokeOrdinaryRole(
        OrganizationRole role,
        IReadOnlyCollection<OrganizationRolePermission> rolePermissions,
        OrganizationMemberSecurityContext targetMember)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(rolePermissions);
        ArgumentNullException.ThrowIfNull(targetMember);

        EnsureCapability(OrganizationCapabilityCatalog.RolesAssign);
        EnsurePrivilegedChangeAllowed();

        if (role.OrganizationId != Organization.Id
            || targetMember.OrganizationId != Organization.Id
            || OrganizationCapabilityCatalog.IsSystemOwnerRole(role))
        {
            throw CreateAccessDenied();
        }

        // Multi-role Owner targets and Admin role reductions require system Owner authority.
        if ((targetMember.IsSystemOwner || OrganizationCapabilityCatalog.IsSystemAdminRole(role))
            && !Actor.IsSystemOwner)
        {
            throw CreateAccessDenied();
        }

        IReadOnlySet<string> targetRolePermissions = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, rolePermissions);
        foreach (string code in targetRolePermissions)
        {
            if (!Actor.EffectivePermissions.Contains(code))
            {
                throw CreateAccessDenied();
            }
        }
    }

    public void EnsureCanOfferRoles(
        string invitationCapabilityCode,
        IReadOnlyCollection<OrganizationRole> offeredRoles,
        IReadOnlyCollection<OrganizationRolePermission> offeredRolePermissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationCapabilityCode);
        ArgumentNullException.ThrowIfNull(offeredRoles);
        ArgumentNullException.ThrowIfNull(offeredRolePermissions);

        EnsureCapability(invitationCapabilityCode);
        EnsureCapability(OrganizationCapabilityCatalog.RolesManage);
        EnsurePrivilegedChangeAllowed();

        if (offeredRoles.Count == 0)
        {
            throw CreateAccessDenied();
        }

        foreach (OrganizationRole role in offeredRoles)
        {
            if (role.OrganizationId != Organization.Id
                || role.RetiredAt is not null
                || OrganizationCapabilityCatalog.IsSystemOwnerRole(role))
            {
                throw CreateAccessDenied();
            }

            IReadOnlySet<string> rolePermissions = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, offeredRolePermissions);
            foreach (string code in rolePermissions)
            {
                if (!OrganizationCapabilityCatalog.IsDelegablePermission(code)
                    || !Actor.EffectivePermissions.Contains(code))
                {
                    throw CreateAccessDenied();
                }
            }
        }
    }

    public void EnsureCanRemoveMember(OrganizationMemberSecurityContext targetMember)
    {
        ArgumentNullException.ThrowIfNull(targetMember);

        EnsureCapability(OrganizationCapabilityCatalog.MembersRemove);
        EnsurePrivilegedChangeAllowed();

        if (targetMember.OrganizationId != Organization.Id)
        {
            throw CreateAccessDenied();
        }

        // Multi-role Owner target uses protected Owner rules.
        if (targetMember.IsSystemOwner)
        {
            if (!Actor.IsSystemOwner)
            {
                throw CreateAccessDenied();
            }

            if (targetMember.IsEligibleActiveMember && ActiveEligibleOwnerCount < 2)
            {
                throw new ConflictException(OwnerContinuityErrorCode, OwnerContinuityMessage);
            }

            return;
        }

        // Removing an Admin membership is Owner-only.
        if (targetMember.IsSystemAdmin)
        {
            if (!Actor.IsSystemOwner)
            {
                throw CreateAccessDenied();
            }

            return;
        }

        // Ordinary removal requires all target effective workspace powers to be within the actor's powers.
        if (!targetMember.EffectivePermissions.IsSubsetOf(Actor.EffectivePermissions))
        {
            throw CreateAccessDenied();
        }
    }

    public void EnsureCanDemoteOwner(OrganizationMemberSecurityContext targetMember)
    {
        ArgumentNullException.ThrowIfNull(targetMember);

        EnsureOwnerAuthority();
        EnsurePrivilegedChangeAllowed();

        if (targetMember.OrganizationId != Organization.Id || !targetMember.IsSystemOwner)
        {
            throw CreateAccessDenied();
        }

        if (targetMember.IsEligibleActiveMember && ActiveEligibleOwnerCount < 2)
        {
            throw new ConflictException(OwnerContinuityErrorCode, OwnerContinuityMessage);
        }
    }

    public static ForbiddenException CreateAccessDenied()
        => new(AccessDeniedErrorCode, AccessDeniedMessage);
}
