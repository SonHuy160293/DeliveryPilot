namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

/// <summary>The immutable result of the creation intent, also returned on an authorized replay.</summary>
public sealed record EngagementCreationSnapshot(
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
    DateTimeOffset CreatedAt,
    bool DuplicateAgreementReference)
{
    public override string ToString() => nameof(EngagementCreationSnapshot);
}
