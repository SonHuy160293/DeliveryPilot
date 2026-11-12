using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tier")]
[JsonDerivedType(typeof(EngagementDetail), EngagementTier.Full)]
public record EngagementDetail(
    Guid Id,
    Guid OrganizationId,
    string ClientDisplayName,
    string? ClientLegalName,
    string? ClientCountryCode,
    string? EngagementName,
    string AgreementReference,
    DateOnly AgreementSignedOn,
    string? DocumentUrl,
    decimal? ContractAmount,
    string? ContractCurrency,
    decimal? PlannedEffortPersonDays,
    string Status,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,
    Guid? ClientOrganizationId,
    DateTimeOffset? ClientLinkedAt,
    Guid? ClientLinkedByUserId,
    bool HasOutstandingLinkCode,
    DateTimeOffset? LinkCodeExpiresAt,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version,
    IReadOnlyList<EngagementContactView> Contacts,
    IReadOnlyList<EngagementMilestoneView> Milestones) : EngagementListEntry
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EngagementProjectView>? Projects { get; init; }

    public override string ToString() => nameof(EngagementDetail);
}
