using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public interface IEngagementMutationStore
{
    Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken);
    Task<bool> HasOtherActiveReferenceAsync(Guid organizationId, string reference, Guid excludeEngagementId, CancellationToken cancellationToken);
    Task<Engagement?> LockAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<EngagementMilestone?> FindMilestoneAsync(Guid organizationId, Guid engagementId, Guid milestoneId, CancellationToken cancellationToken);
    Task<EngagementContact?> FindContactAsync(Guid organizationId, Guid engagementId, Guid contactId, CancellationToken cancellationToken);
    Task<Guid?> DemoteClientProductOwnerAsync(Guid organizationId, Guid engagementId, Guid exceptContactId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
    Task<EngagementLinkCode?> FindOutstandingCodeAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<Guid?> InvalidateOutstandingCodeAsync(Guid organizationId, Guid engagementId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
    void AddLinkCode(EngagementLinkCode code);
    void AddMilestone(EngagementMilestone milestone);
    void AddContact(EngagementContact contact);
    void AddEvent(EngagementEvent entry);
}
