using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.GetEngagement;

public sealed class GetEngagementHandler(EngagementReadAuthorization authorization, IEngagementReadStore store) : IQueryHandler<GetEngagementQuery, EngagementDetail>
{
    public async Task<Result<EngagementDetail>> HandleAsync(GetEngagementQuery request, CancellationToken cancellationToken)
    {
        ReadAccess access = await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        EngagementDetail? detail = await store.GetAsync(request.OrganizationId, request.EngagementId, access, cancellationToken);
        return Result.Success(detail ?? throw new NotFoundException("Engagement.NotFound", "The engagement was not found."));
    }
}
