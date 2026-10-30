namespace DeliveryPilot.Identity.Domain.Models;

public sealed class PasswordCredential
{
    private PasswordCredential()
    {
    }

    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static PasswordCredential Create(Guid userId, string passwordHash, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new PasswordCredential
        {
            UserId = userId,
            PasswordHash = passwordHash,
            CreatedAt = createdAt.ToUniversalTime(),
        };
    }

    public override string ToString() => $"{nameof(PasswordCredential)} {{ UserId = {UserId} }}";
}
