using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public interface IProjectCreationStore
{
    Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken);
    Task<ProjectCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, ProjectCreationDetails details, CancellationToken cancellationToken);
    Task<Engagement?> LockEngagementAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    void Stage(ProjectCreation creation, ProjectEvent entry, Guid userId, Guid key, ProjectCreationDetails details, DateTimeOffset now);
}
