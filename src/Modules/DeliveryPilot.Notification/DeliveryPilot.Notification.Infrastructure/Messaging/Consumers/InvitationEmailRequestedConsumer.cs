using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptInvitationDelivery;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Cqrs;
using MassTransit;

namespace DeliveryPilot.Notification.Infrastructure.Messaging.Consumers;

public sealed class InvitationEmailRequestedConsumer(ISender sender) : IConsumer<OrganizationInvitationEmailRequestedV1>
{
    public async Task Consume(ConsumeContext<OrganizationInvitationEmailRequestedV1> context)
    {
        if (context.MessageId != context.Message.MessageId)
        {
            throw new InvalidOperationException("Message identity mismatch.");
        }

        var result = await sender.SendAsync(new AcceptInvitationDeliveryCommand(context.Message), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Invitation delivery intake rejected.");
        }
    }
}
