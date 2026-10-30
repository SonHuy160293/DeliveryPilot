namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public interface IOrganizationMutationGuard
{
    Task<OrganizationMutationScope> AcquireForNominationAsync(Guid organizationId, Guid nominationId, CancellationToken cancellationToken = default);

    Task<OrganizationMutationScope> AcquireForInvitationAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMutationScope> AcquireAsync(
        Guid organizationId,
        Guid? targetMembershipId = null,
        CancellationToken cancellationToken = default);

    Task<OrganizationMutationScope> AcquireForActorAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid? targetMembershipId = null,
        CancellationToken cancellationToken = default);
}
