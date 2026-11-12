using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptProjectGuestInvitationDelivery;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Cqrs;
using MassTransit;

namespace DeliveryPilot.Notification.Infrastructure.Messaging.Consumers;

public sealed class ProjectGuestInvitationEmailRequestedConsumer(ISender sender) : IConsumer<ProjectGuestInvitationEmailRequestedV1>
{
    public async Task Consume(ConsumeContext<ProjectGuestInvitationEmailRequestedV1> context)
    {
        if (context.MessageId != context.Message.MessageId) { throw new InvalidOperationException("Message identity mismatch."); }
        var result = await sender.SendAsync(new AcceptProjectGuestInvitationDeliveryCommand(context.Message), context.CancellationToken);
        if (result.IsFailure) { throw new InvalidOperationException("Project invitation delivery intake rejected."); }
    }
}
