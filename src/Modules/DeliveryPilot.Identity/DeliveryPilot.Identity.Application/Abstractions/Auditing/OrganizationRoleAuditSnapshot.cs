using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Auditing;

public sealed record OrganizationRoleAuditSnapshot(
    Guid RoleId,
    Guid OrganizationId,
    string Code,
    string Name,
    string? Description,
    string Origin,
    int? TemplateVersion,
    long PermissionSetVersion,
    long Version,
    IReadOnlyList<string> PermissionCodes)
{
    public static OrganizationRoleAuditSnapshot Capture(OrganizationRole role, IEnumerable<string> currentPermissionCodes)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(currentPermissionCodes);
        string[] codes = currentPermissionCodes
            .Select(code => code.Trim())
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        return new OrganizationRoleAuditSnapshot(
            role.Id,
            role.OrganizationId,
            role.Code,
            role.Name,
            role.Description,
            role.Origin,
            role.TemplateVersion,
            role.PermissionSetVersion,
            role.Version,
            codes);
    }
}
