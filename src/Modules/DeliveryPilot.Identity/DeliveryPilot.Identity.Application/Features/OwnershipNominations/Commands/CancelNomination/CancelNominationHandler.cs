using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.CancelNomination;

public sealed class CancelNominationHandler(OwnershipNominationService service) : ICommandHandler<CancelNominationCommand, NominationSummary>
{
    public async Task<Result<NominationSummary>> HandleAsync(CancelNominationCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.RespondAsync(request.OrganizationId, request.NominationId, "cancelled", request.ExpectedVersion, request.Reason, request.CorrelationId, cancellationToken));
}
