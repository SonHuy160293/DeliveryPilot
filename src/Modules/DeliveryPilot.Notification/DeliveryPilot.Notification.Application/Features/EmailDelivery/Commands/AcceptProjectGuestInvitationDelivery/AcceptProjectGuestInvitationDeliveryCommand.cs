using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptProjectGuestInvitationDelivery;

public sealed record AcceptProjectGuestInvitationDeliveryCommand(ProjectGuestInvitationEmailRequestedV1 Event) : ICommand<bool>
{
    public override string ToString() => $"AcceptProjectGuestInvitationDeliveryCommand {Event.MessageId} [REDACTED]";
}
