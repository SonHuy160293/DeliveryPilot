using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public interface IInvitationRecipientStore
{
    Task<InvitationRecipientContext?> ReadAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<InvitationRecipientContext?> AcquireAsync(Guid invitationId, CancellationToken cancellationToken);
    void AddMembership(OrganizationMembership membership);
    void AddAssignment(OrganizationRoleAssignment assignment);
}
