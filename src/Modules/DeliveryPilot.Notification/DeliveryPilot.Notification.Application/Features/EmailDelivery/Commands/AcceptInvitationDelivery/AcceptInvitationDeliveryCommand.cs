using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptInvitationDelivery;

public sealed record AcceptInvitationDeliveryCommand(OrganizationInvitationEmailRequestedV1 Event) : ICommand<bool>
{
    public override string ToString() => $"AcceptInvitationDeliveryCommand {Event.MessageId} [REDACTED]";
}
