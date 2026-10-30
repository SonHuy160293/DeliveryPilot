using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationDeliveryStatusStore
{
    Task RecordAsync(OrganizationInvitationEmailStatusV1 message, CancellationToken cancellationToken);
}
