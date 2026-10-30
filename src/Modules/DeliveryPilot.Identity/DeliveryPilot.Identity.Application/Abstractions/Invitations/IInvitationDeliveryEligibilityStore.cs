using DeliveryPilot.Identity.Contracts;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationDeliveryEligibilityStore
{
    Task<InvitationDeliveryEligibility> GetAsync(Guid deliveryId, CancellationToken cancellationToken);
}
