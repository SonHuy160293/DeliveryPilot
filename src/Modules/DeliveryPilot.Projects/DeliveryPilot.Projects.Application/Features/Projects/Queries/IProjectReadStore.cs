using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public interface IProjectReadStore
{
    Task<IReadOnlyList<ProjectStakeholder>> ListStakeholdersAsync(Guid organizationId, Guid projectId, bool includeRetired, CancellationToken cancellationToken);
    Task<ProjectParticipation?> FindParticipationAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<Project?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
    Task<ProjectPage> ListAsync(Guid organizationId, Guid userId, string kind, string status, int limit, ProjectPageKey? after, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectParticipation>> ListParticipantsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
    Task<ProjectBrief?> FindBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductGoalHistoryEntry>> ListProductGoalHistoryAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
}
