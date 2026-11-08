using DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.DemoteOwner;

public sealed class DemoteOwnerHandler(MembershipChangeService service) : ICommandHandler<DemoteOwnerCommand, MembershipChangeResponse>
{
    public async Task<Result<MembershipChangeResponse>> HandleAsync(DemoteOwnerCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.DemoteOwnerAsync(request.OrganizationId, request.MembershipId, request.ExpectedMembershipVersion, request.Reason!, request.CorrelationId, cancellationToken));
}
