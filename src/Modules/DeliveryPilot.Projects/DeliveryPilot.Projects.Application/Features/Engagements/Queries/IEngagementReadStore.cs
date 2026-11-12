namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public interface IEngagementReadStore
{
    Task<EngagementPage<EngagementListEntry>> ListAsync(Guid organizationId, ReadAccess access, string status, int limit, EngagementPageKey? after, CancellationToken cancellationToken);
    Task<EngagementView?> GetAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken);
    Task<IReadOnlyList<EngagementProjectView>> ListProjectsAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<bool> CanReadAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken);
    Task<EngagementPage<EngagementHistoryEntry>> HistoryAsync(Guid organizationId, Guid engagementId, int limit, EngagementPageKey? after, CancellationToken cancellationToken);
}
