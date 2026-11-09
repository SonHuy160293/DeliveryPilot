namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementDetail(
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
    IReadOnlyList<EngagementMilestoneView> Milestones)
{
    public override string ToString() => nameof(EngagementDetail);
}
