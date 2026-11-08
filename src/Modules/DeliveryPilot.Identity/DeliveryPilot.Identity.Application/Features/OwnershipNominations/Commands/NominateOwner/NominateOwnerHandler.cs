using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.NominateOwner;

public sealed class NominateOwnerHandler(OwnershipNominationService service) : ICommandHandler<NominateOwnerCommand, NominationSummary>
{
    public async Task<Result<NominationSummary>> HandleAsync(NominateOwnerCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.NominateAsync(request.OrganizationId, request.MembershipId, request.CorrelationId, cancellationToken));
}
