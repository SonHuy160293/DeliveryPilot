using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagementHistory;

public sealed class ListEngagementHistoryHandler(EngagementReadAuthorization authorization, IEngagementReadStore store) : IQueryHandler<ListEngagementHistoryQuery, EngagementPage<EngagementHistoryEntry>>
{
    public async Task<Result<EngagementPage<EngagementHistoryEntry>>> HandleAsync(ListEngagementHistoryQuery request, CancellationToken cancellationToken)
    {
        ReadAccess access = await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        EngagementPageKey? key = EngagementCursor.Parse(request.Cursor, request.OrganizationId, request.EngagementId, "history", request.Limit);
        if (!await store.CanReadAsync(request.OrganizationId, request.EngagementId, access, cancellationToken))
        {
            throw new NotFoundException("Engagement.NotFound", "The engagement was not found.");
        }

        EngagementPage<EngagementHistoryEntry> page = await store.HistoryAsync(request.OrganizationId, request.EngagementId, request.Limit, key, cancellationToken);
        return Result.Success(page);
    }
}
