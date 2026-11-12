using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tier")]
[JsonDerivedType(typeof(EngagementDetail), EngagementTier.Full)]
[JsonDerivedType(typeof(EngagementSummary), EngagementTier.Summary)]
public abstract record EngagementListEntry : EngagementView;
