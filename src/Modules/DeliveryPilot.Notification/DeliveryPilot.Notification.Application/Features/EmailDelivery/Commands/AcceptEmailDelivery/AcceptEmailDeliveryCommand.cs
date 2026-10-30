using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;

public sealed record AcceptEmailDeliveryCommand(EmailDeliveryRequestedV1 Event) : ICommand<bool>
{
    public override string ToString() => $"AcceptEmailDeliveryCommand {Event.MessageId} [REDACTED]";
}
