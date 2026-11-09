using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public sealed record CreateEngagementCommand(
    Guid OrganizationId,
    Guid IdempotencyKey,
    string? ClientDisplayName,
    string? AgreementReference,
    DateOnly? AgreementSignedOn,
    string? ClientLegalName = null,
    string? ClientCountryCode = null,
    string? EngagementName = null,
    string? DocumentUrl = null) : ICommand<CreateEngagementResult>
{
    public override string ToString() => nameof(CreateEngagementCommand);
}
