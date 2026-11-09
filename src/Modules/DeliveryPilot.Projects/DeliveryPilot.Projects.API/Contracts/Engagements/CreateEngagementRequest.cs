using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEngagementRequest(
    string? ClientDisplayName,
    string? AgreementReference,
    DateOnly? AgreementSignedOn,
    string? ClientLegalName = null,
    string? ClientCountryCode = null,
    string? EngagementName = null,
    string? DocumentUrl = null)
{
    public override string ToString() => nameof(CreateEngagementRequest);
}
