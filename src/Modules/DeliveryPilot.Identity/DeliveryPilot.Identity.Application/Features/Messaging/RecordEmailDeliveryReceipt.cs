using DeliveryPilot.Identity.Application.Abstractions.Messaging;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Messaging;
public sealed record RecordEmailDeliveryReceiptCommand(EmailDeliveryRecordedV1 Receipt) : ICommand<bool>;
public sealed class RecordEmailDeliveryReceiptHandler(IEmailDeliveryReceiptStore store) : ICommandHandler<RecordEmailDeliveryReceiptCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordEmailDeliveryReceiptCommand request, CancellationToken ct)
    {
        await store.RecordAsync(request.Receipt, ct);
        return Result.Success(true);
    }
}
