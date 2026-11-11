namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed record EngagementLinkPreview(string? EngagementName, string ClientDisplayName)
{
    public override string ToString() => nameof(EngagementLinkPreview);
}
