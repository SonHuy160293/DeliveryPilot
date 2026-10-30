namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public interface IInvitationIssuanceGovernor
{
    Task CheckAsync(OrganizationMutationScope scope, string recipientEmail, string requiredCapability, CancellationToken cancellationToken = default);
}
