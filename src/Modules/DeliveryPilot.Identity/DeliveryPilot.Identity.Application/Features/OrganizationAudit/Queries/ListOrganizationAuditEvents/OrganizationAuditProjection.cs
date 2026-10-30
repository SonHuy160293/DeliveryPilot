using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Domain.Auditing;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public static class OrganizationAuditProjection
{
    public static IReadOnlySet<string> Actions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        IdentityAuditSchemaCatalog.OrganizationCreated,
        IdentityAuditSchemaCatalog.MembershipCreated,
        IdentityAuditSchemaCatalog.MembershipStatusChanged,
        IdentityAuditSchemaCatalog.RoleAssignmentGranted,
        IdentityAuditSchemaCatalog.RoleAssignmentRevoked,
        IdentityAuditSchemaCatalog.RoleAssignmentRegranted,
        IdentityAuditSchemaCatalog.RoleCreated,
        IdentityAuditSchemaCatalog.RolePermissionsChanged,
        IdentityAuditSchemaCatalog.RoleUpdated,
        IdentityAuditSchemaCatalog.RoleRetired,
        IdentityAuditSchemaCatalog.InvitationIssued,
        IdentityAuditSchemaCatalog.InvitationReissued,
        IdentityAuditSchemaCatalog.InvitationResent,
        IdentityAuditSchemaCatalog.InvitationClosed,
        IdentityAuditSchemaCatalog.OwnershipNominationCreated,
        IdentityAuditSchemaCatalog.OwnershipNominationClosed,
    };

    public static JsonElement Changes(string aggregateType, string action, short version, string details)
    {
        try
        {
            // Bound work and output independently of historical writer behavior.
            if (Encoding.UTF8.GetByteCount(details) > 32768)
            {
                throw new ArgumentException("Audit detail projection exceeds its bound.");
            }

            using JsonDocument document = JsonDocument.Parse(details, new JsonDocumentOptions { MaxDepth = 4 });
            IdentityAuditSchemaCatalog.ValidateAndCanonicalizeDetails(aggregateType, action, version, details);
            JsonElement data = document.RootElement;
            object changes = action switch
            {
                IdentityAuditSchemaCatalog.OrganizationCreated => new
                {
                    name = SafeText(data.GetProperty("name").GetString(), 160),
                    status = Text(data, "status"),
                    createdByUserId = Id(data, "createdByUserId"),
                },
                IdentityAuditSchemaCatalog.MembershipCreated or IdentityAuditSchemaCatalog.MembershipStatusChanged => new
                {
                    userId = Id(data, "userId"),
                    previousStatus = data.GetProperty("previousStatus").ValueKind == JsonValueKind.Null ? null : Text(data, "previousStatus"),
                    newStatus = Text(data, "newStatus"),
                },
                IdentityAuditSchemaCatalog.RoleAssignmentGranted => new
                {
                    roleId = Id(data, "roleId"),
                    grantVersion = Number(data, "grantVersion"),
                    grantedByUserId = Id(data, "grantedByUserId"),
                    grantedAt = Time(data, "grantedAt"),
                    expiresAt = NullableTime(data, "expiresAt"),
                    permissionSetVersion = Number(data, "permissionSetVersion"),
                },
                IdentityAuditSchemaCatalog.RoleAssignmentRevoked => new
                {
                    roleId = Id(data, "roleId"),
                    grantVersion = Number(data, "grantVersion"),
                    grantedAt = Time(data, "grantedAt"),
                    expiresAt = NullableTime(data, "expiresAt"),
                    revokedAt = Time(data, "revokedAt"),
                },
                IdentityAuditSchemaCatalog.RoleAssignmentRegranted => new
                {
                    roleId = Id(data, "roleId"),
                    previousGrantVersion = Number(data, "previousGrantVersion"),
                    grantVersion = Number(data, "grantVersion"),
                    grantedByUserId = Id(data, "grantedByUserId"),
                    grantedAt = Time(data, "grantedAt"),
                    expiresAt = NullableTime(data, "expiresAt"),
                    permissionSetVersion = Number(data, "permissionSetVersion"),
                },
                IdentityAuditSchemaCatalog.RoleCreated or IdentityAuditSchemaCatalog.RolePermissionsChanged => new
                {
                    origin = Text(data, "origin"),
                    templateVersion = data.GetProperty("templateVersion").ValueKind == JsonValueKind.Null ? (int?)null : data.GetProperty("templateVersion").GetInt32(),
                    previousPermissionSetVersion = data.GetProperty("previousPermissionSetVersion").ValueKind == JsonValueKind.Null ? (long?)null : Number(data, "previousPermissionSetVersion"),
                    permissionSetVersion = Number(data, "permissionSetVersion"),
                    addedPermissionCodes = Codes(data, "addedPermissionCodes"),
                    removedPermissionCodes = Codes(data, "removedPermissionCodes"),
                    permissionCodes = Codes(data, "permissionCodes"),
                },
                IdentityAuditSchemaCatalog.RoleUpdated => new
                {
                    previousName = SafeText(data.GetProperty("previousName").GetString(), 160),
                    newName = SafeText(data.GetProperty("newName").GetString(), 160),
                    descriptionChanged = data.GetProperty("descriptionChanged").GetBoolean(),
                },
                IdentityAuditSchemaCatalog.RoleRetired => new
                {
                    code = Text(data, "code"),
                    name = SafeText(data.GetProperty("name").GetString(), 160),
                    retiredAt = Time(data, "retiredAt"),
                    permissionSetVersion = Number(data, "permissionSetVersion"),
                },
                IdentityAuditSchemaCatalog.InvitationIssued or IdentityAuditSchemaCatalog.InvitationReissued or IdentityAuditSchemaCatalog.InvitationResent => new
                {
                    issuedByUserId = Id(data, "issuedByUserId"),
                    offeredRoleIds = RoleIds(data),
                    expiresAt = Time(data, "expiresAt"),
                    replacesInvitationId = NullableId(data, "replacesInvitationId"),
                },
                IdentityAuditSchemaCatalog.InvitationClosed => new
                {
                    previousStatus = Text(data, "previousStatus"),
                    newStatus = Text(data, "newStatus"),
                    closedByUserId = NullableId(data, "closedByUserId"),
                    closedAt = Time(data, "closedAt"),
                },
                IdentityAuditSchemaCatalog.OwnershipNominationCreated or IdentityAuditSchemaCatalog.OwnershipNominationClosed => new
                {
                    nominatedByUserId = Id(data, "nominatedByUserId"),
                    previousStatus = data.GetProperty("previousStatus").ValueKind == JsonValueKind.Null ? null : Text(data, "previousStatus"),
                    newStatus = Text(data, "newStatus"),
                    expiresAt = Time(data, "expiresAt"),
                    closedByUserId = NullableId(data, "closedByUserId"),
                    closedAt = NullableTime(data, "closedAt"),
                },
                _ => throw new ArgumentException("Unsupported audit projection."),
            };
            return JsonSerializer.SerializeToElement(changes);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            // Deliberately discard parser exceptions: their messages may include historical payload text.
            throw new InvalidOperationException("Stored audit details cannot be safely projected.");
        }
    }

    public static string? SafeText(string? text, int maximumLength)
    {
        try
        {
            return text?.Length > maximumLength ? null : IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(text, false);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Guid Id(JsonElement data, string name) => data.GetProperty(name).GetGuid();
    private static Guid? NullableId(JsonElement data, string name) => data.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Id(data, name);
    private static long Number(JsonElement data, string name) => data.GetProperty(name).GetInt64();
    private static DateTimeOffset Time(JsonElement data, string name) => data.GetProperty(name).GetDateTimeOffset().ToUniversalTime();
    private static DateTimeOffset? NullableTime(JsonElement data, string name) => data.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Time(data, name);

    private static string Text(JsonElement data, string name)
        => SafeText(data.GetProperty(name).GetString(), 160) ?? throw new ArgumentException("Unsafe audit text.");

    private static string[] Codes(JsonElement data, string name)
    {
        JsonElement array = data.GetProperty(name);
        if (array.GetArrayLength() > 512)
        {
            throw new ArgumentException("Audit permission array exceeds its bound.");
        }

        return array.EnumerateArray().Select(item => SafeText(item.GetString(), 160) ?? throw new ArgumentException("Unsafe audit code.")).ToArray();
    }

    private static Guid[] RoleIds(JsonElement data)
    {
        JsonElement array = data.GetProperty("offeredRoleIds");
        if (array.GetArrayLength() > 512)
        {
            throw new ArgumentException("Audit role array exceeds its bound.");
        }

        return array.EnumerateArray().Select(item => item.GetGuid()).ToArray();
    }
}
