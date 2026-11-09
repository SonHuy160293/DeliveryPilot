using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.ListEngagements;

public sealed class ListEngagementsHandler(EngagementReadAuthorization authorization, IEngagementReadStore store) : IQueryHandler<ListEngagementsQuery, EngagementPage<EngagementDetail>>
{
    public async Task<Result<EngagementPage<EngagementDetail>>> HandleAsync(ListEngagementsQuery request, CancellationToken cancellationToken)
    {
        ReadAccess access = await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        string status = request.Status?.Trim().ToLowerInvariant() ?? "active";
        if (status != "active" && status != "closed" && status != "all")
        {
            throw new BadRequestException("Engagement.InvalidStatus", "The status filter is invalid.");
        }

        EngagementPageKey? key = EngagementCursor.Parse(request.Cursor, request.OrganizationId, Guid.Empty, status, request.Limit);
        EngagementPage<EngagementDetail> page = await store.ListAsync(request.OrganizationId, access, status, request.Limit, key, cancellationToken);
        return Result.Success(page);
    }
}
