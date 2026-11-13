using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProductGoalHistory;

public sealed class ListProductGoalHistoryHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<ListProductGoalHistoryQuery, IReadOnlyList<ProductGoalHistoryEntry>>
{
    public async Task<Result<IReadOnlyList<ProductGoalHistoryEntry>>> HandleAsync(ListProductGoalHistoryQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireAsync(request.OrganizationId, request.ProjectId, identity, ProjectPermissions.ReadProject, cancellationToken);
        return Result.Success(await store.ListProductGoalHistoryAsync(request.OrganizationId, request.ProjectId, cancellationToken));
    }
}
