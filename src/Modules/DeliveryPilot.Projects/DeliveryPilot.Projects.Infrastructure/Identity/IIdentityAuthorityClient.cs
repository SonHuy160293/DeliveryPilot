using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

public interface IIdentityAuthorityClient
{
    Task<IntegrationResult<UserLookupResult>> LookupUsersAsync(UserLookupRequest request, CancellationToken cancellationToken);

    Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken);

    Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(
        OrganizationAuthorityCheckRequest request,
        CancellationToken cancellationToken);
}
