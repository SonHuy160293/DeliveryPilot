using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptInvitationDelivery;

public sealed class AcceptInvitationDeliveryHandler(IEmailDeliveryRepository repository) : ICommandHandler<AcceptInvitationDeliveryCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(AcceptInvitationDeliveryCommand request, CancellationToken cancellationToken)
    {
        request.Event.Validate();
        await repository.AcceptInvitationAsync(request.Event, cancellationToken);
        return Result.Success(true);
    }
}
