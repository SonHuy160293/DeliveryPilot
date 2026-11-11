using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.UpdateEngagementDetails;

public sealed record UpdateEngagementDetailsCommand(
    Guid OrganizationId,
    Guid EngagementId,
    long? ExpectedVersion,
    string? ClientDisplayName,
    string? AgreementReference,
    DateOnly? AgreementSignedOn,
    string? ClientLegalName,
    string? ClientCountryCode,
    string? EngagementName,
    string? DocumentUrl,
    string? Reason) : ICommand<UpdatedEngagementDetails>
{
    public override string ToString() => nameof(UpdateEngagementDetailsCommand);
}
