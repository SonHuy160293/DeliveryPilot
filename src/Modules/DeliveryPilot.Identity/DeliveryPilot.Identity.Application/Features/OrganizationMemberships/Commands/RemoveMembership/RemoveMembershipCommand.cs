using DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.RemoveMembership;

public sealed record RemoveMembershipCommand(Guid OrganizationId, Guid MembershipId, long ExpectedMembershipVersion, string? Reason, string CorrelationId) : ICommand<MembershipChangeResponse>
{
    public override string ToString() => "RemoveMembershipCommand";
}
