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
    public const int CurrentTemplateVersion = 3;
    public const int CurrentCatalogPolicyVersion = 3;

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

    // Catalog policy version 2 (TASK-DP-003-020, ADR-001 D-03): workspace-level entry points for the planned
    // Projects module. They authorize only the entry action, never project content (FR-DP-003-007).
    public const string EngagementsCreate = "organization.engagements.create";
    public const string EngagementsReadAll = "organization.engagements.read-all";
    public const string ProjectsCreate = "organization.projects.create";

    // Catalog policy version 3 (TASK-DP-003-022, FEAT-DP-025 OQ-025-08): edit, close/reopen and client-link
    // authority over engagements. Workspace-level only; it never grants project content (FR-DP-003-007).
    public const string EngagementsManage = "organization.engagements.manage";

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
        EngagementsCreate,
        EngagementsReadAll,
        ProjectsCreate,
        EngagementsManage,
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

    // Version 2 is frozen like version 1: version 1 codes plus the delivery entry points.
    private static readonly HashSet<string> Version2DelegablePermissionSet = new(Version1DelegablePermissionSet, StringComparer.Ordinal)
    {
        "organization.engagements.create",
        "organization.engagements.read-all",
        "organization.projects.create",
    };

    private static readonly HashSet<string> Version2KnownPermissionSet = new(Version2DelegablePermissionSet, StringComparer.Ordinal)
    {
        "organization.ownership.manage",
    };

    // Version 3 is frozen: version 2 codes plus the engagement-manage code.
    private static readonly HashSet<string> Version3DelegablePermissionSet = new(Version2DelegablePermissionSet, StringComparer.Ordinal)
    {
        "organization.engagements.manage",
    };

    private static readonly HashSet<string> Version3KnownPermissionSet = new(Version3DelegablePermissionSet, StringComparer.Ordinal)
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
                && IsClassifiedGrant(role, permission))
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

    // A row is effective only under the frozen classification of the policy version it was granted with;
    // unknown versions fail closed. The current catalog must also still recognize the code.
    private static bool IsClassifiedGrant(OrganizationRole role, OrganizationRolePermission permission)
    {
        string code = permission.PermissionCode;
        bool isOwner = IsSystemOwnerRole(role);
        (HashSet<string> Known, HashSet<string> Delegable)? classification = permission.CatalogPolicyVersion switch
        {
            1 => (Version1KnownPermissionSet, Version1DelegablePermissionSet),
            2 => (Version2KnownPermissionSet, Version2DelegablePermissionSet),
            3 => (Version3KnownPermissionSet, Version3DelegablePermissionSet),
            _ => null,
        };

        if (classification is null)
        {
            return false;
        }

        return isOwner
            ? classification.Value.Known.Contains(code) && IsKnownPermission(code)
            : classification.Value.Delegable.Contains(code) && IsDelegablePermission(code);
    }
}
