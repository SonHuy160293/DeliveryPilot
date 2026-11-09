using System.Buffers.Binary;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

/// <summary>An opaque keyset cursor bound to its workspace, query and filter.</summary>
public static class EngagementCursor
{
    public static EngagementPageKey? Parse(string? cursor, Guid organizationId, Guid engagementId, string scope, int limit)
    {
        if (limit is < 1 or > 100 || cursor is { Length: > 128 } || cursor == string.Empty)
        {
            throw Invalid();
        }

        if (cursor is null)
        {
            return null;
        }

        try
        {
            if (cursor.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            {
                throw Invalid();
            }

            string base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            byte[] bytes = Convert.FromBase64String(base64);
            if (bytes.Length != 59 || bytes[0] != 1 || bytes[1] != Scope(scope)
                || new Guid(bytes.AsSpan(3, 16)) != organizationId || new Guid(bytes.AsSpan(19, 16)) != engagementId)
            {
                throw Invalid();
            }

            long ticks = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(35, 8));
            var id = new Guid(bytes.AsSpan(43, 16));
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || id == Guid.Empty || bytes[2] != 0)
            {
                throw Invalid();
            }

            return new EngagementPageKey(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (FormatException)
        {
            throw Invalid();
        }
    }

    public static string Encode(Guid organizationId, Guid engagementId, string scope, DateTimeOffset time, Guid id)
    {
        var bytes = new byte[59];
        bytes[0] = 1;
        bytes[1] = Scope(scope);
        organizationId.TryWriteBytes(bytes.AsSpan(3, 16));
        engagementId.TryWriteBytes(bytes.AsSpan(19, 16));
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(35, 8), time.ToUniversalTime().Ticks);
        id.TryWriteBytes(bytes.AsSpan(43, 16));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte Scope(string scope) => scope switch
    {
        "active" => 1,
        "closed" => 2,
        "all" => 3,
        "history" => 4,
        _ => throw Invalid(),
    };

    private static BadRequestException Invalid() => new("Engagement.InvalidPage", "The paging parameters are invalid.");
}
