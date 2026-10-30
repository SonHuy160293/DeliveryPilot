using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.RecordDeliveryStatus;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Cqrs;
using MassTransit;

namespace DeliveryPilot.Identity.Infrastructure.Messaging;

public sealed class InvitationDeliveryStatusConsumer(ISender sender) : IConsumer<OrganizationInvitationEmailStatusV1>
{
    public async Task Consume(ConsumeContext<OrganizationInvitationEmailStatusV1> context)
    {
        if (context.MessageId != context.Message.MessageId)
        {
            throw new InvalidOperationException("Message identity mismatch.");
        }

        var result = await sender.SendAsync(new RecordDeliveryStatusCommand(context.Message), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Invitation delivery status was rejected.");
        }
    }
}
