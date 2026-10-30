using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.RecordDeliveryStatus;

public sealed class RecordDeliveryStatusHandler(IInvitationDeliveryStatusStore store) : ICommandHandler<RecordDeliveryStatusCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordDeliveryStatusCommand request, CancellationToken cancellationToken)
    {
        request.Event.Validate();
        await store.RecordAsync(request.Event, cancellationToken);
        return Result.Success(true);
    }
}
