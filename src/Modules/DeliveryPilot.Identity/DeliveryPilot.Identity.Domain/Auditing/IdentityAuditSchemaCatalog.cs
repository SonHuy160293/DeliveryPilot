using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeliveryPilot.Identity.Domain.Auditing;

public static class IdentityAuditSchemaCatalog
{
    public const string OrganizationAggregate = "Organization";
    public const string OrganizationMembershipAggregate = "OrganizationMembership";
    public const string OrganizationRoleAggregate = "OrganizationRole";
    public const string OrganizationRoleAssignmentAggregate = "OrganizationRoleAssignment";
    public const string OrganizationInvitationAggregate = "OrganizationInvitation";
    public const string OrganizationOwnershipNominationAggregate = "OrganizationOwnershipNomination";

    public const string OrganizationCreated = "organization.created";
    public const string MembershipCreated = "organization.membership.created";
    public const string MembershipStatusChanged = "organization.membership.status-changed";
    public const string RoleAssignmentGranted = "organization.role-assignment.granted";
    public const string RoleAssignmentRevoked = "organization.role-assignment.revoked";
    public const string RoleAssignmentRegranted = "organization.role-assignment.regranted";
    public const string RoleCreated = "organization.role.created";
    public const string RolePermissionsChanged = "organization.role.permissions-changed";
    public const string RoleUpdated = "organization.role.updated";
    public const string RoleRetired = "organization.role.retired";
    public const string InvitationIssued = "organization.invitation.issued";
    public const string InvitationReissued = "organization.invitation.reissued";
    public const string InvitationResent = "organization.invitation.resent";
    public const string InvitationClosed = "organization.invitation.closed";
    public const string OwnershipNominationCreated = "organization.ownership-nomination.created";
    public const string OwnershipNominationClosed = "organization.ownership-nomination.closed";

    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "primaryEmail",
        "recipientEmail",
        "token",
        "rawToken",
        "invitationToken",
        "tokenVerifier",
        "refreshTokenVerifier",
        "verifier",
        "password",
        "passwordHash",
        "secret",
        "link",
        "invitationLink",
        "emailBody",
        "body",
        "rawPayload",
        "requestPayload",
        "authorization",
    };

    private static readonly Dictionary<string, (string AggregateType, HashSet<string> AllowedFields)> EventSchemas = new(StringComparer.Ordinal)
    {
        [OrganizationCreated] = (OrganizationAggregate, ["name", "status", "createdByUserId"]),
        [MembershipCreated] = (OrganizationMembershipAggregate, ["userId", "previousStatus", "newStatus"]),
        [MembershipStatusChanged] = (OrganizationMembershipAggregate, ["userId", "previousStatus", "newStatus"]),
        [RoleAssignmentGranted] = (OrganizationRoleAssignmentAggregate, ["roleId", "grantVersion", "grantedByUserId", "grantedAt", "expiresAt", "permissionSetVersion"]),
        [RoleAssignmentRevoked] = (OrganizationRoleAssignmentAggregate, ["roleId", "grantVersion", "grantedAt", "expiresAt", "revokedAt"]),
        [RoleAssignmentRegranted] = (OrganizationRoleAssignmentAggregate, ["roleId", "previousGrantVersion", "grantVersion", "grantedByUserId", "grantedAt", "expiresAt", "permissionSetVersion"]),
        [RoleCreated] = (OrganizationRoleAggregate, ["origin", "templateVersion", "previousPermissionSetVersion", "permissionSetVersion", "addedPermissionCodes", "removedPermissionCodes", "permissionCodes"]),
        [RolePermissionsChanged] = (OrganizationRoleAggregate, ["origin", "templateVersion", "previousPermissionSetVersion", "permissionSetVersion", "addedPermissionCodes", "removedPermissionCodes", "permissionCodes"]),
        [RoleUpdated] = (OrganizationRoleAggregate, ["previousName", "newName", "descriptionChanged"]),
        [RoleRetired] = (OrganizationRoleAggregate, ["code", "name", "retiredAt", "permissionSetVersion"]),
        [InvitationIssued] = (OrganizationInvitationAggregate, ["issuedByUserId", "offeredRoleIds", "expiresAt", "replacesInvitationId"]),
        [InvitationReissued] = (OrganizationInvitationAggregate, ["issuedByUserId", "offeredRoleIds", "expiresAt", "replacesInvitationId"]),
        [InvitationResent] = (OrganizationInvitationAggregate, ["issuedByUserId", "offeredRoleIds", "expiresAt", "replacesInvitationId"]),
        [InvitationClosed] = (OrganizationInvitationAggregate, ["previousStatus", "newStatus", "closedByUserId", "closedAt"]),
        [OwnershipNominationCreated] = (OrganizationOwnershipNominationAggregate, ["nominatedByUserId", "previousStatus", "newStatus", "expiresAt", "closedByUserId", "closedAt"]),
        [OwnershipNominationClosed] = (OrganizationOwnershipNominationAggregate, ["nominatedByUserId", "previousStatus", "newStatus", "expiresAt", "closedByUserId", "closedAt"]),
    };

    public static (string CanonicalDetailsJson, bool RequiresReasonByPayload) ValidateAndCanonicalizeDetails(
        string aggregateType,
        string eventType,
        short detailsSchemaVersion,
        string detailsJson)
    {
        if (detailsSchemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(detailsSchemaVersion), "Unsupported audit details schema version.");
        }

        if (!EventSchemas.TryGetValue(eventType, out (string AggregateType, HashSet<string> AllowedFields) schema))
        {
            throw new ArgumentException("Unsupported audit event type.", nameof(eventType));
        }

        if (!string.Equals(schema.AggregateType, aggregateType, StringComparison.Ordinal))
        {
            throw new ArgumentException("Audit aggregate type does not match the event schema.", nameof(aggregateType));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(detailsJson);
        using JsonDocument document = JsonDocument.Parse(detailsJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Audit details must be a JSON object.", nameof(detailsJson));
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (ForbiddenPropertyNames.Contains(property.Name))
            {
                throw new ArgumentException("Audit details contain a forbidden sensitive or personal field.", nameof(detailsJson));
            }

            if (!schema.AllowedFields.Contains(property.Name))
            {
                throw new ArgumentException("Audit details contain an unallowlisted field for this event type.", nameof(detailsJson));
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new ArgumentException("Audit details contain duplicate property names.", nameof(detailsJson));
            }
        }

        if (properties.Count != schema.AllowedFields.Count)
        {
            throw new ArgumentException("Audit details are missing required schema fields.", nameof(detailsJson));
        }

        bool requiresReason = ValidateEventSpecificFields(eventType, properties);
        return (JsonSerializer.Serialize(root), requiresReason);
    }

    public static string? ValidateAndNormalizeReason(string? reason, bool requiresReason)
    {
        if (reason is null)
        {
            if (requiresReason)
            {
                throw new ArgumentException("A nonblank audit reason is required for this operation.", nameof(reason));
            }

            return null;
        }

        string trimmed = reason.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Audit reason must not be blank when supplied.", nameof(reason));
        }

        if (trimmed.Length > 1000)
        {
            throw new ArgumentException("Audit reason must not exceed 1000 characters.", nameof(reason));
        }

        if (ContainsForbiddenReasonPattern(trimmed))
        {
            throw new ArgumentException("Audit reason contains forbidden credential, token, or email material.", nameof(reason));
        }

        return trimmed;
    }

    private static bool ValidateEventSpecificFields(string eventType, IReadOnlyDictionary<string, JsonElement> properties)
    {
        switch (eventType)
        {
            case OrganizationCreated:
                RequireNonBlankString(properties["name"], 160);
                RequireNonBlankString(properties["status"], 16);
                RequireNonEmptyGuid(properties["createdByUserId"]);
                return false;

            case MembershipCreated:
                RequireNonEmptyGuid(properties["userId"]);
                RequireNull(properties["previousStatus"]);
                RequireNonBlankString(properties["newStatus"], 16);
                return false;

            case MembershipStatusChanged:
            {
                RequireNonEmptyGuid(properties["userId"]);
                string previousStatus = RequireNonBlankString(properties["previousStatus"], 16);
                string newStatus = RequireNonBlankString(properties["newStatus"], 16);
                if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Membership status-changed audit event requires distinct before and after states.");
                }

                return string.Equals(newStatus, "removed", StringComparison.Ordinal);
            }

            case RoleAssignmentGranted:
                RequireNonEmptyGuid(properties["roleId"]);
                RequirePositiveInt64(properties["grantVersion"]);
                RequireNonEmptyGuid(properties["grantedByUserId"]);
                RequireTimestamp(properties["grantedAt"]);
                RequireNullableTimestamp(properties["expiresAt"]);
                RequirePositiveInt64(properties["permissionSetVersion"]);
                return false;

            case RoleAssignmentRevoked:
                RequireNonEmptyGuid(properties["roleId"]);
                RequirePositiveInt64(properties["grantVersion"]);
                RequireTimestamp(properties["grantedAt"]);
                RequireNullableTimestamp(properties["expiresAt"]);
                RequireTimestamp(properties["revokedAt"]);
                return false;

            case RoleAssignmentRegranted:
            {
                RequireNonEmptyGuid(properties["roleId"]);
                long previousGrantVersion = RequirePositiveInt64(properties["previousGrantVersion"]);
                long grantVersion = RequirePositiveInt64(properties["grantVersion"]);
                if (grantVersion <= previousGrantVersion)
                {
                    throw new ArgumentException("Regrant audit event requires GrantVersion greater than PreviousGrantVersion.");
                }

                RequireNonEmptyGuid(properties["grantedByUserId"]);
                RequireTimestamp(properties["grantedAt"]);
                RequireNullableTimestamp(properties["expiresAt"]);
                RequirePositiveInt64(properties["permissionSetVersion"]);
                return false;
            }

            case RoleCreated:
                ValidateRolePermissionSnapshot(properties, expectPreviousVersion: false);
                return false;

            case RolePermissionsChanged:
                ValidateRolePermissionSnapshot(properties, expectPreviousVersion: true);
                return false;

            case RoleUpdated:
                RequireNonBlankString(properties["previousName"], 160);
                RequireNonBlankString(properties["newName"], 160);
                if (properties["descriptionChanged"].ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new ArgumentException("Audit role update requires a boolean descriptionChanged flag.");
                }

                return false;

            case RoleRetired:
                RequireNonBlankString(properties["code"], 80);
                RequireNonBlankString(properties["name"], 160);
                RequireTimestamp(properties["retiredAt"]);
                RequirePositiveInt64(properties["permissionSetVersion"]);
                return true;

            case InvitationIssued:
            case InvitationReissued:
            case InvitationResent:
                RequireNonEmptyGuid(properties["issuedByUserId"]);
                RequireNonEmptyUniqueGuidArray(properties["offeredRoleIds"]);
                RequireTimestamp(properties["expiresAt"]);
                RequireNullableGuid(properties["replacesInvitationId"]);
                return false;

            case InvitationClosed:
            {
                string previousStatus = RequireNonBlankString(properties["previousStatus"], 16);
                string newStatus = RequireNonBlankString(properties["newStatus"], 16);
                if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Invitation closed audit event requires distinct before and after states.");
                }

                RequireNullableGuid(properties["closedByUserId"]);
                RequireTimestamp(properties["closedAt"]);
                return string.Equals(newStatus, "cancelled", StringComparison.Ordinal);
            }

            case OwnershipNominationCreated:
            case OwnershipNominationClosed:
                RequireNonEmptyGuid(properties["nominatedByUserId"]);
                RequireNullableNonBlankString(properties["previousStatus"], 16);
                RequireNonBlankString(properties["newStatus"], 16);
                RequireTimestamp(properties["expiresAt"]);
                RequireNullableGuid(properties["closedByUserId"]);
                RequireNullableTimestamp(properties["closedAt"]);
                return false;

            default:
                throw new ArgumentException("Unsupported audit event type.");
        }
    }

    private static void ValidateRolePermissionSnapshot(IReadOnlyDictionary<string, JsonElement> properties, bool expectPreviousVersion)
    {
        string origin = RequireNonBlankString(properties["origin"], 16);
        if (origin is not ("system" or "custom"))
        {
            throw new ArgumentException("Role audit origin must be system or custom.");
        }

        if (origin == "system")
        {
            if (properties["templateVersion"].ValueKind != JsonValueKind.Number ||
                !properties["templateVersion"].TryGetInt32(out int tv) ||
                tv <= 0)
            {
                throw new ArgumentException("System role audit event requires a positive templateVersion.");
            }
        }
        else if (properties["templateVersion"].ValueKind != JsonValueKind.Null)
        {
            throw new ArgumentException("Custom role audit event must have a null templateVersion.");
        }

        long permissionSetVersion = RequirePositiveInt64(properties["permissionSetVersion"]);
        if (expectPreviousVersion)
        {
            long previousVersion = RequirePositiveInt64(properties["previousPermissionSetVersion"]);
            if (permissionSetVersion <= previousVersion)
            {
                throw new ArgumentException("Permission-set change audit event requires an incremented PermissionSetVersion.");
            }
        }
        else
        {
            RequireNull(properties["previousPermissionSetVersion"]);
        }

        RequireUniquePermissionCodeArray(properties["addedPermissionCodes"]);
        RequireUniquePermissionCodeArray(properties["removedPermissionCodes"]);
        RequireUniquePermissionCodeArray(properties["permissionCodes"]);
    }

    private static string RequireNonBlankString(JsonElement element, int maxLength)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Audit detail field must be a string.");
        }

        string? value = element.GetString()?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            throw new ArgumentException("Audit detail string field is empty or exceeds maximum length.");
        }

        return value;
    }

    private static void RequireNullableNonBlankString(JsonElement element, int maxLength)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            RequireNonBlankString(element, maxLength);
        }
    }

    private static void RequireNull(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            throw new ArgumentException("Audit detail field must be null for initial creation.");
        }
    }

    private static Guid RequireNonEmptyGuid(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String ||
            !element.TryGetGuid(out Guid value) ||
            value == Guid.Empty)
        {
            throw new ArgumentException("Audit detail field must be a non-empty UUID.");
        }

        return value;
    }

    private static void RequireNullableGuid(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            RequireNonEmptyGuid(element);
        }
    }

    private static long RequirePositiveInt64(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt64(out long value) ||
            value <= 0)
        {
            throw new ArgumentException("Audit detail field must be a positive integer.");
        }

        return value;
    }

    private static void RequireTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String ||
            !element.TryGetDateTimeOffset(out _))
        {
            throw new ArgumentException("Audit detail field must be a valid timestamp.");
        }
    }

    private static void RequireNullableTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            RequireTimestamp(element);
        }
    }

    private static void RequireUniquePermissionCodeArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Audit permission code list must be a JSON array.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in element.EnumerateArray())
        {
            string code = RequireNonBlankString(item, 160);
            if (!seen.Add(code))
            {
                throw new ArgumentException("Audit permission code array must not contain duplicates.");
            }
        }
    }

    private static void RequireNonEmptyUniqueGuidArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Audit offeredRoleIds must be a JSON array.");
        }

        var seen = new HashSet<Guid>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            Guid roleId = RequireNonEmptyGuid(item);
            if (!seen.Add(roleId))
            {
                throw new ArgumentException("Audit offeredRoleIds must not contain duplicates.");
            }
        }

        if (seen.Count == 0)
        {
            throw new ArgumentException("Audit offeredRoleIds must not be empty.");
        }
    }

    private static bool ContainsForbiddenReasonPattern(string value)
    {
        return value.Contains("http://", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(value, @"[^\s<>(),;:]+@[^\s<>(),;:]+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100))
               || value.Contains("https://", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
               || value.Contains("token=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("secret=", StringComparison.OrdinalIgnoreCase);
    }
}
