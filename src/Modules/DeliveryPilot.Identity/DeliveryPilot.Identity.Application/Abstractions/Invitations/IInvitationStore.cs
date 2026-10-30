using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationStore
{
    Task<InvitationSummary?> FindSummaryAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken);
    Task<OrganizationReadPage<InvitationSummary>> ListAsync(Guid organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> FindAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken);
    Task<OrganizationInvitation?> FindPendingAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationInvitationRoleOffer>> GetOffersAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationRole>> GetRolesAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationRolePermission>> GetPermissionsAsync(Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    Task<string?> GetRecipientMembershipStatusAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken);
    Task CloseForReplacementAsync(OrganizationInvitation invitation, string status, Guid? actorId, DateTimeOffset now, string? reason, CancellationToken cancellationToken);
    void Add(OrganizationInvitation invitation, IReadOnlyCollection<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery);
    void AddDelivery(OrganizationInvitationDelivery delivery);
}
