using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Projects.Mutations;

public interface IProjectMutationStore
{
    Task<Project?> LockProjectAsync(Guid organizationId, Guid projectId, bool exclusive, CancellationToken cancellationToken);
    Task<ProjectBrief?> LockBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
    Task<Engagement?> LockEngagementAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<ProjectStakeholder?> LockStakeholderAsync(Guid organizationId, Guid projectId, Guid stakeholderId, CancellationToken cancellationToken);
    Task<bool> IsActiveParticipantAsync(Guid organizationId, Guid projectId, Guid participantId, CancellationToken cancellationToken);
    void AddStakeholder(ProjectStakeholder stakeholder);
    void AddEvent(ProjectEvent entry);
}
