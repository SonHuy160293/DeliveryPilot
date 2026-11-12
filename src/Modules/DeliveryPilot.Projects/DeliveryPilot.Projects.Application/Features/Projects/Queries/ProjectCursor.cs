using System.Buffers.Binary;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public static class ProjectCursor
{
    public static ProjectPageKey? Parse(string? cursor, Guid organizationId, Guid userId, string kind, string status, int limit)
    {
        if (limit is < 1 or > 100 || cursor == string.Empty || cursor is { Length: > 128 }) { throw Invalid(); }
        if (cursor is null) { return null; }
        try
        {
            if (cursor.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) { throw Invalid(); }
            string value = cursor.Replace('-', '+').Replace('_', '/');
            byte[] bytes = Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '='));
            if (bytes.Length != 59 || bytes[0] != 1 || bytes[1] != Kind(kind) || bytes[2] != Status(status) || new Guid(bytes.AsSpan(3, 16)) != organizationId || new Guid(bytes.AsSpan(19, 16)) != userId) { throw Invalid(); }
            long ticks = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(35, 8));
            Guid id = new(bytes.AsSpan(43, 16));
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || id == Guid.Empty) { throw Invalid(); }
            return new ProjectPageKey(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (FormatException) { throw Invalid(); }
    }

    public static string Encode(Guid organizationId, Guid userId, string kind, string status, DateTimeOffset createdAt, Guid id)
    {
        var bytes = new byte[59];
        bytes[0] = 1;
        bytes[1] = Kind(kind);
        bytes[2] = Status(status);
        organizationId.TryWriteBytes(bytes.AsSpan(3, 16));
        userId.TryWriteBytes(bytes.AsSpan(19, 16));
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(35, 8), createdAt.ToUniversalTime().Ticks);
        id.TryWriteBytes(bytes.AsSpan(43, 16));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte Kind(string kind) => kind switch { "Member" => 1, "Guest" => 2, _ => throw Invalid() };
    private static byte Status(string status) => status switch { "all" => 0, "draft" => 1, "active" => 2, _ => throw Invalid() };
    private static BadRequestException Invalid() => new("Project.InvalidPage", "The paging parameters are invalid.");
}
