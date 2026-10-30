using DeliveryPilot.Identity.Application.Features.Messaging;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Cqrs;
using MassTransit;

namespace DeliveryPilot.Identity.Infrastructure.Messaging;
public sealed class EmailDeliveryRecordedConsumer(ISender sender) : IConsumer<EmailDeliveryRecordedV1>
{
    public async Task Consume(ConsumeContext<EmailDeliveryRecordedV1> context)
    {
        if (context.MessageId != context.Message.MessageId)
        {
            throw new InvalidOperationException("Message identity mismatch.");
        }

        var result = await sender.SendAsync(new RecordEmailDeliveryReceiptCommand(context.Message), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Receipt rejected.");
        }
    }
}
