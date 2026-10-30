using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationDeliveryStager
{
    Task StageAsync(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery, InvitationProof proof, string correlationId, CancellationToken cancellationToken);
}
