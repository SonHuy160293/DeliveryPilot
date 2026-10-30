using DeliveryPilot.SharedKernel.Abstractions.Domain;

namespace DeliveryPilot.Identity.Domain.Models;

public sealed class User : Entity<Guid>
{
    private User()
        : base(Guid.CreateVersion7())
    {
        UpdatedAt = CreatedAt;
    }

    public string PrimaryEmail { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public string Locale { get; private set; } = "en";
    public string TimeZone { get; private set; } = "UTC";
    public string Status { get; private set; } = "pending";
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static User Create(
        string primaryEmail,
        string displayName,
        string? avatarUrl = null,
        string locale = "en",
        string timeZone = "UTC",
        string status = "pending",
        DateTimeOffset? emailVerifiedAt = null,
        DateTimeOffset? lastLoginAt = null)
    {
        string email = NormalizeEmail(primaryEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        string name = displayName.Trim();
        if (name.Length > 160)
        {
            throw new ArgumentException("DisplayName must not exceed 160 characters.", nameof(displayName));
        }

        return new User
        {
            PrimaryEmail = email,
            DisplayName = name,
            AvatarUrl = avatarUrl,
            Locale = locale,
            TimeZone = timeZone,
            Status = status,
            EmailVerifiedAt = emailVerifiedAt?.ToUniversalTime(),
            LastLoginAt = lastLoginAt?.ToUniversalTime(),
        };
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        string normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 320)
        {
            throw new ArgumentException("Email must contain between 3 and 320 characters.", nameof(email));
        }

        return normalized;
    }

    public void RecordLogin(DateTimeOffset loggedInAt)
    {
        DateTimeOffset instant = loggedInAt.ToUniversalTime();
        LastLoginAt = instant;
        UpdatedAt = instant;
    }
}
