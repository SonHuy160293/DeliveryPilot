using DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.DemoteOwner;

public sealed record DemoteOwnerCommand(Guid OrganizationId, Guid MembershipId, long ExpectedMembershipVersion, string? Reason, string CorrelationId) : ICommand<MembershipChangeResponse>
{
    public override string ToString() => "DemoteOwnerCommand";
}
