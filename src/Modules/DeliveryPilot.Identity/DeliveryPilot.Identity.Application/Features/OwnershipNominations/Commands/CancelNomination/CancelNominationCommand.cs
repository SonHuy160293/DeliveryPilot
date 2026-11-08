using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.CancelNomination;

public sealed record CancelNominationCommand(Guid OrganizationId, Guid NominationId, long ExpectedVersion, string? Reason, string CorrelationId) : ICommand<NominationSummary>
{
    public override string ToString() => "CancelNominationCommand";
}
