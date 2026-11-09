using DeliveryPilot.AspNetCore.HttpClient.Abstractions;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

public sealed class IdentityAuthorityClient(IIntegrationHttpClient<IIdentityAuthorityClient> client) : IIdentityAuthorityClient
{
    public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
        => client.PostAsync<SessionCheckRequest, SessionCheckResult>(
            IdentityIntegration.SessionChecksEndpoint,
            request,
            cancellationToken);

    public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(
        OrganizationAuthorityCheckRequest request,
        CancellationToken cancellationToken)
        => client.PostAsync<OrganizationAuthorityCheckRequest, OrganizationAuthorityCheckResult>(
            IdentityIntegration.OrganizationChecksEndpoint,
            request,
            cancellationToken);
}
