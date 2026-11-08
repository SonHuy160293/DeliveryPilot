using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.AcceptNomination;

public sealed record AcceptNominationCommand(Guid OrganizationId, Guid NominationId, long ExpectedVersion, string CorrelationId) : ICommand<NominationSummary>
{
    public override string ToString() => "AcceptNominationCommand";
}
