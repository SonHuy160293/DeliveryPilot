namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public interface IEngagementReadStore
{
    Task<EngagementPage<EngagementDetail>> ListAsync(Guid organizationId, ReadAccess access, string status, int limit, EngagementPageKey? after, CancellationToken cancellationToken);
    Task<EngagementDetail?> GetAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken);
    Task<bool> CanReadAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken);
    Task<EngagementPage<EngagementHistoryEntry>> HistoryAsync(Guid organizationId, Guid engagementId, int limit, EngagementPageKey? after, CancellationToken cancellationToken);
}
