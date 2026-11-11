namespace DeliveryPilot.Projects.Domain.Models.Engagements;

public sealed record EngagementFieldChange(object? Previous, object? New)
{
    public override string ToString() => nameof(EngagementFieldChange);
}
