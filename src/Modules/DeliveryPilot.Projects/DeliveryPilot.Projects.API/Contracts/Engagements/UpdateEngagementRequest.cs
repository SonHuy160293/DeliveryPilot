using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEngagementRequest(
    long? ExpectedVersion,
    string? ClientDisplayName,
    string? AgreementReference,
    DateOnly? AgreementSignedOn,
    string? ClientLegalName,
    string? ClientCountryCode,
    string? EngagementName,
    string? DocumentUrl,
    string? Reason)
{
    public override string ToString() => nameof(UpdateEngagementRequest);
}
