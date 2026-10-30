namespace DeliveryPilot.Notification.Application.Abstractions;

public interface IInvitationDispatchEligibility
{
    Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken);
}
