namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementPage<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public override string ToString() => nameof(EngagementPage<T>);
}
