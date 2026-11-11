using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public interface IClientLinkStore
{
    // Deliberate verifier lookup across vendors: the client does not know the owning workspace.
    Task<EngagementLinkCode?> FindCodeByVerifierAsync(byte[] verifier, CancellationToken cancellationToken);
    Task<Engagement?> LockEngagementForLinkAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<EngagementLinkCode?> ReloadCodeAsync(Guid organizationId, Guid engagementId, Guid codeId, CancellationToken cancellationToken);
    Task<Engagement?> LockLinkedAsync(Guid clientOrganizationId, Guid engagementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LinkedEngagement>> ListAsync(Guid clientOrganizationId, CancellationToken cancellationToken);
    void AddEvent(EngagementEvent entry);
}

public sealed record LinkedEngagement(Guid Id, string? EngagementName, string ClientDisplayName);
