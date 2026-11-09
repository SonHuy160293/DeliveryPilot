using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public interface IEngagementCreationStore
{
    Task LockAttemptAsync(Guid organizationId, Guid userId, Guid key, CancellationToken cancellationToken);
    Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken);
    Task<EngagementCreationSnapshot?> FindReplayAsync(Guid organizationId, Guid userId, Guid key, EngagementCreationDetails details, CancellationToken cancellationToken);
    Task<bool> HasActiveReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken);
    void Stage(Engagement engagement, EngagementEvent entry, Guid userId, Guid key, EngagementCreationDetails details, DateTimeOffset now);
}
