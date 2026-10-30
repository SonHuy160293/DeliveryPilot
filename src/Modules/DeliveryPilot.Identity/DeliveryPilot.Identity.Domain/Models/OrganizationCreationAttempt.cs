using System.Text.Json;

namespace DeliveryPilot.Identity.Domain.Models;

public sealed class OrganizationCreationAttempt
{
    private static readonly HashSet<string> SnapshotFields = new(StringComparer.Ordinal)
    {
        "organizationId", "name", "status", "membershipId", "roles", "version", "membershipVersion", "resultKind", "snapshotAt",
    };

    private OrganizationCreationAttempt() { }

    public Guid UserId { get; private set; }
    public Guid AttemptId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public byte[] RequestFingerprint { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ReplayUntil { get; private set; }
    public short ResponseSchemaVersion { get; private set; }
    public string ResultSnapshot { get; private set; } = null!;

    public static OrganizationCreationAttempt Create(
        Guid userId,
        Guid attemptId,
        Guid organizationId,
        byte[] fingerprint,
        DateTimeOffset createdAt,
        string resultSnapshot)
    {
        if (userId == Guid.Empty || attemptId == Guid.Empty || organizationId == Guid.Empty || fingerprint.Length != 32)
        {
            throw new ArgumentException("A creation attempt requires valid identifiers and a SHA-256 fingerprint.");
        }

        var attempt = new OrganizationCreationAttempt
        {
            UserId = userId,
            AttemptId = attemptId,
            OrganizationId = organizationId,
            RequestFingerprint = (byte[])fingerprint.Clone(),
            CreatedAt = createdAt.ToUniversalTime(),
            ReplayUntil = createdAt.ToUniversalTime().AddHours(24),
            ResponseSchemaVersion = 1,
            ResultSnapshot = resultSnapshot,
        };
        attempt.ValidateSnapshot();
        return attempt;
    }

    public void ValidateSnapshot()
    {
        if (ResponseSchemaVersion != 1 || RequestFingerprint.Length != 32 || ReplayUntil != CreatedAt.AddHours(24)
            || string.IsNullOrWhiteSpace(ResultSnapshot) || System.Text.Encoding.UTF8.GetByteCount(ResultSnapshot) > 8192)
        {
            throw new InvalidOperationException("The creation receipt is invalid.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(ResultSnapshot);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The creation snapshot is invalid.");
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!SnapshotFields.Contains(property.Name) || !names.Add(property.Name))
                {
                    throw new InvalidOperationException("The creation snapshot is invalid.");
                }
            }

            if (names.Count != SnapshotFields.Count
                || !root.GetProperty("organizationId").TryGetGuid(out Guid organizationId) || organizationId != OrganizationId
                || !root.GetProperty("membershipId").TryGetGuid(out Guid membershipId) || membershipId == Guid.Empty
                || root.GetProperty("name").ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(root.GetProperty("name").GetString())
                || root.GetProperty("name").GetString()!.Length > 160
                || root.GetProperty("status").GetString() != "active"
                || root.GetProperty("resultKind").GetString() != "creationSnapshot"
                || !root.GetProperty("version").TryGetInt64(out long version) || version != 1
                || !root.GetProperty("membershipVersion").TryGetInt64(out long membershipVersion) || membershipVersion != 1
                || !root.GetProperty("snapshotAt").TryGetDateTimeOffset(out DateTimeOffset snapshotAt)
                || snapshotAt.ToUniversalTime() != CreatedAt
                || root.GetProperty("roles").ValueKind != JsonValueKind.Array
                || root.GetProperty("roles").GetArrayLength() != 1
                || root.GetProperty("roles")[0].GetString() != "Owner")
            {
                throw new InvalidOperationException("The creation snapshot is invalid.");
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidOperationException("The creation receipt is invalid.", exception);
        }
    }
}
