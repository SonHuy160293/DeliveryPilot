using DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.RemoveMembership;

public sealed class RemoveMembershipHandler(MembershipChangeService service) : ICommandHandler<RemoveMembershipCommand, MembershipChangeResponse>
{
    public async Task<Result<MembershipChangeResponse>> HandleAsync(RemoveMembershipCommand request, CancellationToken cancellationToken)
        => Result.Success(await service.RemoveAsync(request.OrganizationId, request.MembershipId, request.ExpectedMembershipVersion, request.Reason!, request.CorrelationId, cancellationToken));
}
