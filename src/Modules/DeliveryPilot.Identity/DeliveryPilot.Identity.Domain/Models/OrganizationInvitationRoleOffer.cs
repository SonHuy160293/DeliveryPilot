using System.Text.Json;

namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationInvitationRoleOffer
{
    private OrganizationInvitationRoleOffer() { }

    public Guid OrganizationId { get; private set; }
    public Guid InvitationId { get; private set; }
    public Guid RoleId { get; private set; }
    public string OfferedRoleName { get; private set; } = null!;
    public long OfferedPermissionSetVersion { get; private set; }
    public JsonElement OfferedPermissionCodes { get; private set; }

    public static OrganizationInvitationRoleOffer Create(Guid organizationId, Guid invitationId, Guid roleId, string offeredRoleName, long offeredPermissionSetVersion, JsonElement offeredPermissionCodes)
    {
        if (organizationId == Guid.Empty || invitationId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new ArgumentException("Organization, invitation and role IDs must not be empty.");
        }

        if (offeredPermissionSetVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offeredPermissionSetVersion));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(offeredRoleName);
        string normalizedName = offeredRoleName.Trim();
        if (normalizedName.Length > 160)
        {
            throw new ArgumentException("Offered role name must not exceed 160 characters.", nameof(offeredRoleName));
        }

        if (offeredPermissionCodes.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Permission codes must be an array.", nameof(offeredPermissionCodes));
        }

        string? previous = null;
        foreach (JsonElement item in offeredPermissionCodes.EnumerateArray())
        {
            string? code = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (string.IsNullOrWhiteSpace(code) || code.Length > 160 || code != code.Trim() ||
                (previous is not null && string.CompareOrdinal(previous, code) >= 0))
            {
                throw new ArgumentException("Permission codes must be trimmed, unique, bounded and in ordinal order.", nameof(offeredPermissionCodes));
            }

            previous = code;
        }

        return new OrganizationInvitationRoleOffer
        {
            OrganizationId = organizationId,
            InvitationId = invitationId,
            RoleId = roleId,
            OfferedRoleName = normalizedName,
            OfferedPermissionSetVersion = offeredPermissionSetVersion,
            OfferedPermissionCodes = offeredPermissionCodes.Clone(),
        };
    }
}
