using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.DeclineNomination;

public sealed class DeclineNominationHandler(OwnershipNominationService service) : ICommandHandler<DeclineNominationCommand, NominationSummary>
{
    public async Task<Result<NominationSummary>> HandleAsync(DeclineNominationCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.RespondAsync(request.OrganizationId, request.NominationId, "declined", request.ExpectedVersion, null, request.CorrelationId, cancellationToken));
}
