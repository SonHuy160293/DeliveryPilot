using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.PreviewInvitation;

public sealed record PreviewInvitationQuery(Guid InvitationId, string? Proof) : IQuery<InvitationPreview>
{
    public override string ToString() => "PreviewInvitationQuery { Proof = [REDACTED] }";
}
