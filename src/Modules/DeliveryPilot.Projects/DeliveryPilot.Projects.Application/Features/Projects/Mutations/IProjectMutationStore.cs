using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Mutations;

public interface IProjectMutationStore
{
    Task<Project?> LockProjectAsync(Guid organizationId, Guid projectId, bool exclusive, CancellationToken cancellationToken);
    Task<ProjectBrief?> LockBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);
    void AddEvent(ProjectEvent entry);
}
