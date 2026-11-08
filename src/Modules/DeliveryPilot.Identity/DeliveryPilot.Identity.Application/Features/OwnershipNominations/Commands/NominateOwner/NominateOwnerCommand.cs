using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.NominateOwner;

public sealed record NominateOwnerCommand(Guid OrganizationId, Guid MembershipId, string CorrelationId) : ICommand<NominationSummary>
{
    public override string ToString() => "NominateOwnerCommand";
}
