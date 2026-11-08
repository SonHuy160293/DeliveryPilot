using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.DeclineNomination;

public sealed record DeclineNominationCommand(Guid OrganizationId, Guid NominationId, long ExpectedVersion, string CorrelationId) : ICommand<NominationSummary>
{
    public override string ToString() => "DeclineNominationCommand";
}
