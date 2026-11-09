using System.Net;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Tests.Support;

internal sealed class FakeIdentityAuthorityClient : IIdentityAuthorityClient
{
    public IntegrationResult<SessionCheckResult> Session { get; set; } =
        IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(true), HttpStatusCode.OK);

    public IntegrationResult<OrganizationAuthorityCheckResult>? Organization { get; set; }

    public OrganizationAuthorityCheckRequest? LastOrganizationRequest { get; private set; }

    public static IntegrationResult<T> Unavailable<T>()
        => IntegrationResult<T>.Failure(
            HttpStatusCode.ServiceUnavailable,
            new IntegrationError("Integration.Http503", "Unavailable.", IntegrationFailureCategory.Downstream));

    public Task<IntegrationResult<SessionCheckResult>> CheckSessionAsync(SessionCheckRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Session);

    public Task<IntegrationResult<OrganizationAuthorityCheckResult>> CheckOrganizationAuthorityAsync(
        OrganizationAuthorityCheckRequest request,
        CancellationToken cancellationToken)
    {
        LastOrganizationRequest = request;
        return Task.FromResult(Organization ?? Unavailable<OrganizationAuthorityCheckResult>());
    }
}
