using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

public interface IIdentityAuthorityClient
{
    Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken);

    Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(
        OrganizationAuthorityCheckRequest request,
        CancellationToken cancellationToken);
}
