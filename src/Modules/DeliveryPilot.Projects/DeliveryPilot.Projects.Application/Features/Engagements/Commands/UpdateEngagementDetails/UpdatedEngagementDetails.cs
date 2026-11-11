using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.UpdateEngagementDetails;

public sealed record UpdatedEngagementDetails(
    Guid Id,
    string Status,
    long Version,
    string ClientDisplayName,
    string AgreementReference,
    DateOnly AgreementSignedOn,
    string? ClientLegalName,
    string? ClientCountryCode,
    string? EngagementName,
    string? DocumentUrl,
    DateTimeOffset UpdatedAt,
    bool DuplicateAgreementReference)
{
    public static UpdatedEngagementDetails From(Engagement engagement, bool duplicate)
        => new(engagement.Id, engagement.Status, engagement.Version, engagement.ClientDisplayName, engagement.AgreementReference, engagement.AgreementSignedOn,
            engagement.ClientLegalName, engagement.ClientCountryCode, engagement.EngagementName, engagement.DocumentUrl, engagement.UpdatedAt, duplicate);

    public override string ToString() => nameof(UpdatedEngagementDetails);
}
