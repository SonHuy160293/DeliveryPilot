using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.RecordEmailWebhook;

public sealed class RecordEmailWebhookHandler(IEmailDeliveryRepository repository) : ICommandHandler<RecordEmailWebhookCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordEmailWebhookCommand request, CancellationToken ct)
    {
        await repository.RecordWebhookAsync(request.Event, ct);
        return Result.Success(true);
    }
}
