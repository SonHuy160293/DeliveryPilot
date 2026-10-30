using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.AcceptInvitation;

public sealed record AcceptInvitationCommand(Guid InvitationId, string? Proof, long ExpectedVersion, string CorrelationId) : ICommand<InvitationAcceptance>
{
    public override string ToString() => "AcceptInvitationCommand { Proof = [REDACTED] }";
}
