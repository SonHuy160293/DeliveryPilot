using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public interface IClientLinkStore
{
    Task<Engagement?> LockLinkedAsync(Guid clientOrganizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LinkedEngagement>> ListAsync(Guid clientOrganizationId, CancellationToken cancellationToken);
    void AddEvent(EngagementEvent entry);
}

public sealed record LinkedEngagement(Guid Id, string? EngagementName, string ClientDisplayName);
