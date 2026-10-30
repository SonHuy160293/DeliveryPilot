using DeliveryPilot.Identity.Domain.Authorization;

namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationRolePermission
{
    private OrganizationRolePermission()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid RoleId { get; private set; }

    public string PermissionCode { get; private set; } = null!;

    public int CatalogPolicyVersion { get; private set; }

    public static OrganizationRolePermission Create(Guid organizationId, Guid roleId, string permissionCode)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId must not be empty.", nameof(organizationId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("RoleId must not be empty.", nameof(roleId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        string code = permissionCode.Trim();
        if (code.Length > 160)
        {
            throw new ArgumentException("PermissionCode must not exceed 160 characters.", nameof(permissionCode));
        }

        return new OrganizationRolePermission
        {
            OrganizationId = organizationId,
            RoleId = roleId,
            PermissionCode = code,
            CatalogPolicyVersion = OrganizationCapabilityCatalog.CurrentCatalogPolicyVersion,
        };
    }
}
