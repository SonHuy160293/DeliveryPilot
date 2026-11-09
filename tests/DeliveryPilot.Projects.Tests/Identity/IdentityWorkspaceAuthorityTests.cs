using System.Net;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Tests.Identity;

public sealed class IdentityWorkspaceAuthorityTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task AllowedResultMapsAuthorityAndForwardsRequest()
    {
        var membershipId = Guid.NewGuid();
        var client = new FakeIdentityAuthorityClient { Organization = Allowed(membershipId, WorkspacePermissions.ProjectsCreate) };

        WorkspaceAuthority authority = await new IdentityWorkspaceAuthority(client)
            .RequireAsync(UserId, OrganizationId, SessionId, WorkspacePermissions.ProjectsCreate, CancellationToken.None);

        Assert.Equal(OrganizationId, authority.OrganizationId);
        Assert.Equal(membershipId, authority.MembershipId);
        Assert.True(authority.HasPermission(WorkspacePermissions.ProjectsCreate));
        Assert.False(authority.HasPermission(WorkspacePermissions.EngagementsReadAll));
        Assert.Equal(
            new OrganizationAuthorityCheckRequest(UserId, OrganizationId, SessionId, WorkspacePermissions.ProjectsCreate),
            client.LastOrganizationRequest);
    }

    [Fact]
    public async Task DeniedDecisionIsForbidden()
    {
        var client = new FakeIdentityAuthorityClient
        {
            Organization = IntegrationResult<OrganizationAuthorityCheckResult>.Success(
                OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UtcNow),
                HttpStatusCode.OK),
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => new IdentityWorkspaceAuthority(client)
            .RequireAsync(UserId, OrganizationId, SessionId, WorkspacePermissions.ProjectsCreate, CancellationToken.None));
    }

    [Fact]
    public async Task MissingRequiredPermissionIsForbidden()
    {
        var client = new FakeIdentityAuthorityClient { Organization = Allowed(Guid.NewGuid(), WorkspacePermissions.EngagementsCreate) };

        await Assert.ThrowsAsync<ForbiddenException>(() => new IdentityWorkspaceAuthority(client)
            .RequireAsync(UserId, OrganizationId, SessionId, WorkspacePermissions.ProjectsCreate, CancellationToken.None));
    }

    [Fact]
    public async Task UnavailableIdentityFailsClosed()
    {
        var client = new FakeIdentityAuthorityClient();

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => new IdentityWorkspaceAuthority(client)
            .RequireAsync(UserId, OrganizationId, SessionId, null, CancellationToken.None));
    }

    [Fact]
    public async Task AllowedResultForAnotherOrganizationFailsClosed()
    {
        var client = new FakeIdentityAuthorityClient { Organization = Allowed(Guid.NewGuid(), organizationId: Guid.NewGuid()) };

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => new IdentityWorkspaceAuthority(client)
            .RequireAsync(UserId, OrganizationId, SessionId, null, CancellationToken.None));
    }

    private static IntegrationResult<OrganizationAuthorityCheckResult> Allowed(
        Guid membershipId,
        string? permission = null,
        Guid? organizationId = null)
        => IntegrationResult<OrganizationAuthorityCheckResult>.Success(
            new OrganizationAuthorityCheckResult(
                AuthorityDecision.Allowed,
                DateTimeOffset.UtcNow,
                organizationId ?? OrganizationId,
                OrganizationVersion: 1,
                MembershipId: membershipId,
                MembershipVersion: 1,
                IsOwner: false,
                IsAdmin: false,
                EffectivePermissions: permission is null ? [] : [permission]),
            HttpStatusCode.OK);
}
