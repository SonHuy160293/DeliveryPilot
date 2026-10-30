namespace DeliveryPilot.Identity.Domain.Models;

public sealed class Organization
{
    private Organization() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset StatusChangedAt { get; private set; }
    public Guid? StatusChangedByUserId { get; private set; }
    public string? StatusReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static Organization Create(string name, Guid createdByUserId, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string normalizedName = name.Trim();
        if (normalizedName.Length > 160)
        {
            throw new ArgumentException("Name must not exceed 160 characters.", nameof(name));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator must not be empty.", nameof(createdByUserId));
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new Organization
        {
            Id = Guid.CreateVersion7(),
            Name = normalizedName,
            Status = "active",
            CreatedByUserId = createdByUserId,
            StatusChangedAt = instant,
            StatusChangedByUserId = createdByUserId,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }
}
