using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Invitations;

public sealed record InvitationRecipientContext(
    Organization Organization,
    OrganizationInvitation Invitation,
    User Recipient,
    OrganizationMembership? Membership,
    IReadOnlyList<OrganizationInvitationRoleOffer> Offers,
    IReadOnlyList<OrganizationRole> Roles,
    IReadOnlyList<OrganizationRolePermission> Permissions,
    IReadOnlyList<OrganizationRoleAssignment> Assignments,
    OrganizationMutationScope? Sponsor,
    bool Superseded)
{
    public override string ToString() => nameof(InvitationRecipientContext);
}
