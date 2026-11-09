using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public sealed record EngagementCreationDetails(
    string ClientDisplayName,
    string AgreementReference,
    DateOnly AgreementSignedOn,
    string? ClientLegalName,
    string? ClientCountryCode,
    string? EngagementName,
    string? DocumentUrl)
{
    public static EngagementCreationDetails Normalize(CreateEngagementCommand request)
        => new(
            EngagementFields.RequiredText(request.ClientDisplayName, 200, nameof(request.ClientDisplayName)),
            EngagementFields.RequiredText(request.AgreementReference, 100, nameof(request.AgreementReference)),
            EngagementFields.CalendarDate(request.AgreementSignedOn ?? default, nameof(request.AgreementSignedOn)),
            EngagementFields.OptionalText(request.ClientLegalName, 200, nameof(request.ClientLegalName)),
            EngagementFields.CountryCode(request.ClientCountryCode),
            EngagementFields.OptionalText(request.EngagementName, 200, nameof(request.EngagementName)),
            EngagementFields.DocumentUrl(request.DocumentUrl));

    public override string ToString() => nameof(EngagementCreationDetails);
}
