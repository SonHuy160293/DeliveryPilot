using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;

public sealed class AcceptEmailDeliveryHandler(IEmailDeliveryRepository repository) : ICommandHandler<AcceptEmailDeliveryCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(AcceptEmailDeliveryCommand request, CancellationToken ct)
    {
        await repository.AcceptAsync(request.Event, ct);
        return Result.Success(true);
    }
}
