using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.DeclineInvitation;

public sealed record DeclineInvitationCommand(Guid InvitationId, string? Proof, long ExpectedVersion, string CorrelationId) : ICommand<InvitationDecline>
{
    public override string ToString() => "DeclineInvitationCommand { Proof = [REDACTED] }";
}
