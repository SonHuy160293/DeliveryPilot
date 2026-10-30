using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.AcceptNomination;

public sealed class AcceptNominationHandler(OwnershipNominationService service) : ICommandHandler<AcceptNominationCommand, NominationSummary>
{
    public async Task<Result<NominationSummary>> HandleAsync(AcceptNominationCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.RespondAsync(request.OrganizationId, request.NominationId, "accepted", request.ExpectedVersion, null, request.CorrelationId, cancellationToken));
}
