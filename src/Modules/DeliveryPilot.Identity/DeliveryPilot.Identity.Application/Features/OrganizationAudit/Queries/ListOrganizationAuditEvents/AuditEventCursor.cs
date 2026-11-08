using System.Text;
using System.Text.Json;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public static class AuditEventCursor
{
    // Compact arrays keep the complete scope/filter binding inside the 1,024-character contract.
    public static string Encode(AuditEventFilter filter, AuditPageKey key)
    {
        string json = JsonSerializer.Serialize(new object?[]
        {
            1, filter.Mode, filter.OrganizationId.ToString("N"),
            new object?[] { filter.ActorUserId?.ToString("N"), filter.MembershipId?.ToString("N"), filter.TargetId?.ToString("N"), filter.Action, filter.From, filter.To },
            filter.MembershipId.HasValue
                ? new object?[] { key.MembershipVersion, key.EventOrdinal, key.Id.ToString("N") }
                : [key.OccurredAt, key.Id.ToString("N")],
        });
        return Base64(Encoding.UTF8.GetBytes(json));
    }

    public static AuditPageKey? Decode(string? cursor, AuditEventFilter filter)
    {
        if (cursor is null)
        {
            return null;
        }

        try
        {
            if (cursor.Length is < 1 or > 1024 || cursor.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            {
                throw Invalid();
            }

            string encoded = cursor.Replace('-', '+').Replace('_', '/');
            byte[] bytes = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
            if (Base64(bytes) != cursor)
            {
                throw Invalid();
            }

            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 5 || root[0].GetInt32() != 1 ||
                root[1].GetString() != filter.Mode || root[2].GetString() != filter.OrganizationId.ToString("N"))
            {
                throw Invalid();
            }

            string expected = JsonSerializer.Serialize(new object?[] { filter.ActorUserId?.ToString("N"), filter.MembershipId?.ToString("N"), filter.TargetId?.ToString("N"), filter.Action, filter.From, filter.To });
            if (root[3].GetRawText() != expected)
            {
                throw Invalid();
            }

            JsonElement tuple = root[4];
            if (tuple.ValueKind != JsonValueKind.Array || tuple.GetArrayLength() != (filter.MembershipId.HasValue ? 3 : 2))
            {
                throw Invalid();
            }

            int idIndex = filter.MembershipId.HasValue ? 2 : 1;
            if (!Guid.TryParseExact(tuple[idIndex].GetString(), "N", out Guid id) || id == Guid.Empty)
            {
                throw Invalid();
            }

            if (filter.MembershipId.HasValue)
            {
                long version = tuple[0].GetInt64();
                int ordinal = tuple[1].GetInt32();
                if (version <= 0 || ordinal <= 0)
                {
                    throw Invalid();
                }

                return new AuditPageKey(id, MembershipVersion: version, EventOrdinal: ordinal);
            }

            string timestamp = tuple[0].GetString() ?? throw Invalid();
            if (!timestamp.EndsWith("+00:00", StringComparison.Ordinal) && !timestamp.EndsWith('Z'))
            {
                throw Invalid();
            }

            DateTimeOffset occurredAt = tuple[0].GetDateTimeOffset();
            if ((filter.From.HasValue && occurredAt < filter.From.Value) || (filter.To.HasValue && occurredAt >= filter.To.Value))
            {
                throw Invalid();
            }

            return new AuditPageKey(id, occurredAt);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or OverflowException or ArgumentException)
        {
            throw Invalid();
        }
    }

    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static CqrsValidationException Invalid() => new(["Invalid audit cursor for this scope and filter."]);
}
