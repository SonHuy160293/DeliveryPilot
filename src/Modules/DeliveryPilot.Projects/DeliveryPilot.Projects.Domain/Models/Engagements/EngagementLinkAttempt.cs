namespace DeliveryPilot.Projects.Domain.Models.Engagements;

/// <summary>A failed preview/confirmation; contains no submitted proof or engagement reference.</summary>
public sealed class EngagementLinkAttempt
{
    private EngagementLinkAttempt() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ActingOrganizationId { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }

    public static EngagementLinkAttempt Create(Guid userId, Guid actingOrganizationId, DateTimeOffset attemptedAt)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = EngagementFields.NonEmptyId(userId, nameof(userId)),
            ActingOrganizationId = EngagementFields.NonEmptyId(actingOrganizationId, nameof(actingOrganizationId)),
            AttemptedAt = attemptedAt.ToUniversalTime(),
        };
}
