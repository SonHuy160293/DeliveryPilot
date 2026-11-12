using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptProjectGuestInvitationDelivery;

public sealed class AcceptProjectGuestInvitationDeliveryHandler(IProjectGuestInvitationRepository repository) : ICommandHandler<AcceptProjectGuestInvitationDeliveryCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(AcceptProjectGuestInvitationDeliveryCommand request, CancellationToken cancellationToken)
    {
        request.Event.Validate();
        await repository.AcceptAsync(request.Event, cancellationToken);
        return Result.Success(true);
    }
}
