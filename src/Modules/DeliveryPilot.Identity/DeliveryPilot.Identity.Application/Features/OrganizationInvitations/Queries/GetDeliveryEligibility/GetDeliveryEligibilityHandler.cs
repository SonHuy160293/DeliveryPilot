using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetDeliveryEligibility;

public sealed class GetDeliveryEligibilityHandler(IInvitationDeliveryEligibilityStore store) : IQueryHandler<GetDeliveryEligibilityQuery, InvitationDeliveryEligibility>
{
    public async Task<Result<InvitationDeliveryEligibility>> HandleAsync(GetDeliveryEligibilityQuery request, CancellationToken cancellationToken)
        => Result.Success(await store.GetAsync(request.DeliveryMessageId, cancellationToken));
}
