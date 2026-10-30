using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Domain.Authorization;

public sealed record OrganizationBuiltInRoleSeed(
    OrganizationRole OwnerRole,
    OrganizationRole AdminRole,
    OrganizationRole MemberRole,
    IReadOnlyList<OrganizationRole> Roles,
    IReadOnlyList<OrganizationRolePermission> Permissions);

public sealed record EffectiveOrganizationAuthority(
    bool IsSystemOwner,
    bool IsSystemAdmin,
    IReadOnlySet<string> EffectivePermissions,
    IReadOnlyList<OrganizationRole> EffectiveRoles);

public static class OrganizationCapabilityCatalog
{
    public const string SystemOrigin = "system";
    public const string CustomOrigin = "custom";
    public const int CurrentTemplateVersion = 1;
    public const int CurrentCatalogPolicyVersion = 1;

    public const string OwnerRoleCode = "owner";
    public const string AdminRoleCode = "admin";
    public const string MemberRoleCode = "member";

    public const string OwnerRoleName = "Owner";
    public const string AdminRoleName = "Admin";
    public const string MemberRoleName = "Member";

    public const string MembersRead = "organization.members.read";
    public const string MembersRemove = "organization.members.remove";
    public const string InvitationsRead = "organization.invitations.read";
    public const string InvitationsIssue = "organization.invitations.issue";
    public const string InvitationsResend = "organization.invitations.resend";
    public const string InvitationsReissue = "organization.invitations.reissue";
    public const string InvitationsCancel = "organization.invitations.cancel";
    public const string RolesManage = "organization.roles.manage";
    public const string RolesAssign = "organization.roles.assign";
    public const string AuditRead = "organization.audit.read";
    public const string OwnershipManage = "organization.ownership.manage";

    private static readonly string[] OrderedDelegablePermissionCodes =
    [
        MembersRead,
        MembersRemove,
        InvitationsRead,
        InvitationsIssue,
        InvitationsResend,
        InvitationsReissue,
        InvitationsCancel,
        RolesManage,
        RolesAssign,
        AuditRead,
    ];

    private static readonly string[] OrderedAllPermissionCodes =
    [
        .. OrderedDelegablePermissionCodes,
        OwnershipManage,
    ];

    private static readonly HashSet<string> DelegablePermissionSet = new(OrderedDelegablePermissionCodes, StringComparer.Ordinal);
    private static readonly HashSet<string> AllPermissionSet = new(OrderedAllPermissionCodes, StringComparer.Ordinal);

    // Version 1 is a historical grant classification, not an alias of the current catalog.
    // New classifications require a separately reviewed policy version and grant operation.
    private static readonly HashSet<string> Version1DelegablePermissionSet = new(StringComparer.Ordinal)
    {
        "organization.members.read",
        "organization.members.remove",
        "organization.invitations.read",
        "organization.invitations.issue",
        "organization.invitations.resend",
        "organization.invitations.reissue",
        "organization.invitations.cancel",
        "organization.roles.manage",
        "organization.roles.assign",
        "organization.audit.read",
    };

    private static readonly HashSet<string> Version1KnownPermissionSet = new(Version1DelegablePermissionSet, StringComparer.Ordinal)
    {
        "organization.ownership.manage",
    };

    private static readonly HashSet<string> ReservedBuiltInIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        OwnerRoleCode,
        AdminRoleCode,
        MemberRoleCode,
    };

    public static IReadOnlyList<string> DelegablePermissionCodes => OrderedDelegablePermissionCodes;

    public static IReadOnlyList<string> AllPermissionCodes => OrderedAllPermissionCodes;

    public static bool IsDelegablePermission(string? permissionCode)
        => !string.IsNullOrWhiteSpace(permissionCode) && DelegablePermissionSet.Contains(permissionCode);

    public static bool IsKnownPermission(string? permissionCode)
        => !string.IsNullOrWhiteSpace(permissionCode) && AllPermissionSet.Contains(permissionCode);

    public static bool IsReservedBuiltInRoleCodeOrName(string? value)
        => !string.IsNullOrWhiteSpace(value) && ReservedBuiltInIdentifiers.Contains(value.Trim());

    public static string FormatCustomRoleCode(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("RoleId must not be empty.", nameof(roleId));
        }

        return $"role-{roleId:N}";
    }

    public static bool IsSystemOwnerRole(OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return string.Equals(role.Origin, SystemOrigin, StringComparison.Ordinal)
            && string.Equals(role.Code, OwnerRoleCode, StringComparison.Ordinal);
    }

    public static bool IsSystemAdminRole(OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return string.Equals(role.Origin, SystemOrigin, StringComparison.Ordinal)
            && string.Equals(role.Code, AdminRoleCode, StringComparison.Ordinal);
    }

    public static bool IsSystemMemberRole(OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return string.Equals(role.Origin, SystemOrigin, StringComparison.Ordinal)
            && string.Equals(role.Code, MemberRoleCode, StringComparison.Ordinal);
    }

    public static OrganizationBuiltInRoleSeed CreateBuiltInRoles(Guid organizationId, DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId must not be empty.", nameof(organizationId));
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        var ownerRole = OrganizationRole.Create(
            organizationId,
            OwnerRoleCode,
            OwnerRoleName,
            SystemOrigin,
            CurrentTemplateVersion,
            createdByUserId: null,
            instant,
            "Protected workspace owner role.");

        var adminRole = OrganizationRole.Create(
            organizationId,
            AdminRoleCode,
            AdminRoleName,
            SystemOrigin,
            CurrentTemplateVersion,
            createdByUserId: null,
            instant,
            "Built-in workspace administrator role.");

        var memberRole = OrganizationRole.Create(
            organizationId,
            MemberRoleCode,
            MemberRoleName,
            SystemOrigin,
            CurrentTemplateVersion,
            createdByUserId: null,
            instant,
            "Built-in workspace member role.");

        var permissions = new List<OrganizationRolePermission>(OrderedAllPermissionCodes.Length + OrderedDelegablePermissionCodes.Length);
        foreach (string code in OrderedAllPermissionCodes)
        {
            permissions.Add(OrganizationRolePermission.Create(organizationId, ownerRole.Id, code));
        }

        foreach (string code in OrderedDelegablePermissionCodes)
        {
            permissions.Add(OrganizationRolePermission.Create(organizationId, adminRole.Id, code));
        }

        return new OrganizationBuiltInRoleSeed(
            ownerRole,
            adminRole,
            memberRole,
            [ownerRole, adminRole, memberRole],
            permissions);
    }

    public static IReadOnlySet<string> GetEffectiveRolePermissions(
        OrganizationRole role,
        IReadOnlyCollection<OrganizationRolePermission> rolePermissions)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(rolePermissions);

        if (role.RetiredAt is not null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (IsSystemMemberRole(role))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var effective = new HashSet<string>(StringComparer.Ordinal);
        foreach (OrganizationRolePermission permission in rolePermissions)
        {
            if (permission.OrganizationId == role.OrganizationId
                && permission.RoleId == role.Id
                && permission.CatalogPolicyVersion == 1
                && (IsSystemOwnerRole(role)
                    ? Version1KnownPermissionSet.Contains(permission.PermissionCode) && IsKnownPermission(permission.PermissionCode)
                    : Version1DelegablePermissionSet.Contains(permission.PermissionCode) && IsDelegablePermission(permission.PermissionCode)))
            {
                effective.Add(permission.PermissionCode);
            }
        }

        return effective;
    }

    public static EffectiveOrganizationAuthority ComputeEffectiveAuthority(
        Guid organizationId,
        IReadOnlyCollection<OrganizationRole> activeRoles,
        IReadOnlyCollection<OrganizationRolePermission> rolePermissions)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId must not be empty.", nameof(organizationId));
        }

        ArgumentNullException.ThrowIfNull(activeRoles);
        ArgumentNullException.ThrowIfNull(rolePermissions);

        var scopedRoles = activeRoles
            .Where(role => role.OrganizationId == organizationId && role.RetiredAt is null)
            .OrderBy(role => role.CreatedAt)
            .ThenBy(role => role.Id)
            .ToList();

        bool isSystemOwner = false;
        bool isSystemAdmin = false;
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (OrganizationRole role in scopedRoles)
        {
            if (IsSystemOwnerRole(role))
            {
                isSystemOwner = true;
            }
            else if (IsSystemAdminRole(role))
            {
                isSystemAdmin = true;
            }

            foreach (string code in GetEffectiveRolePermissions(role, rolePermissions))
            {
                permissions.Add(code);
            }
        }

        return new EffectiveOrganizationAuthority(
            isSystemOwner,
            isSystemAdmin,
            permissions,
            scopedRoles);
    }
}
