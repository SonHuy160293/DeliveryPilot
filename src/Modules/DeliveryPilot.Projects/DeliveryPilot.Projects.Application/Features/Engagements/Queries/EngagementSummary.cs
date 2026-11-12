using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

// The response envelope holds object, so the concrete root also declares its discriminator.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tier")]
[JsonDerivedType(typeof(EngagementSummary), EngagementTier.Summary)]
public record EngagementSummary(
    Guid Id,
    string ClientDisplayName,
    string? EngagementName,
    string AgreementReference,
    string Status,
    IReadOnlyList<EngagementMilestoneView> Milestones,
    long Version) : EngagementListEntry
{
    public override string ToString() => nameof(EngagementSummary);
}
