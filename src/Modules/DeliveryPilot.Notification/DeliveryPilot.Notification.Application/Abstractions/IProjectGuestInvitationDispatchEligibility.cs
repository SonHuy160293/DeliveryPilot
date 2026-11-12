namespace DeliveryPilot.Notification.Application.Abstractions;

public interface IProjectGuestInvitationDispatchEligibility
{
    Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken);
}
