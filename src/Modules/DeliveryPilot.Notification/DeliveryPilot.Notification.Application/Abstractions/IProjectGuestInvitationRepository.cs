using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Notification.Application.Abstractions;

public interface IProjectGuestInvitationRepository
{
    Task AcceptAsync(ProjectGuestInvitationEmailRequestedV1 message, CancellationToken cancellationToken);
}
