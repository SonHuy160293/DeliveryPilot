using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyMembership;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetOrganizationMember;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyOrganizations;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.ListOrganizationMembers;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.SharedKernel.Exceptions;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationReadAuthorizationTests
{
    [Fact]
    public async Task GeneralReadsDenyBeforeLookingUpTarget()
    {
        var authorization = new DeniedAuthorization();
        var store = new TrackingStore();
        Guid organizationId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetOrganizationMemberQueryHandler(authorization, store).HandleAsync(new(organizationId, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ListOrganizationMembersQueryHandler(authorization, store).HandleAsync(new(organizationId, "active", 50, null), CancellationToken.None));
        Assert.False(store.WasCalled);
        Assert.Equal(OrganizationCapabilityCatalog.MembersRead, authorization.LastCapability);
    }

    [Fact]
    public async Task OwnReadUsesAuthorizedMembershipAndDoesNotRequireGeneralRead()
    {
        var authorization = new DeniedAuthorization();
        var store = new TrackingStore();
        Guid organizationId = Guid.NewGuid();
        var result = await new GetMyMembershipQueryHandler(authorization, store).HandleAsync(new(organizationId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(authorization.MembershipId, store.MembershipId);
        Assert.Null(authorization.LastCapability);
        Assert.Equal("member", Assert.Single(result.Value.Roles).Code);
    }

    [Fact]
    public async Task HandlersMapMemberAndWorkspacePagesIncludingRolesAndCursors()
    {
        var authorization = new DeniedAuthorization { AllowGeneralRead = true };
        var store = new TrackingStore();
        Guid organizationId = Guid.NewGuid();
        var member = await new GetOrganizationMemberQueryHandler(authorization, store).HandleAsync(new(organizationId, authorization.MembershipId), CancellationToken.None);
        Assert.Equal("Reader", member.Value.DisplayName);
        Assert.Equal("member", Assert.Single(member.Value.Roles).Code);
        var members = await new ListOrganizationMembersQueryHandler(authorization, store).HandleAsync(new(organizationId, "active", 50, null), CancellationToken.None);
        Assert.Equal("members-next", members.Value.NextCursor);
        Assert.Equal("member", Assert.Single(Assert.Single(members.Value.Items).Roles).Code);
        var workspaces = await new GetMyOrganizationsQueryHandler(authorization, store).HandleAsync(new(50, null), CancellationToken.None);
        Assert.Equal("workspaces-next", workspaces.Value.NextCursor);
        Assert.Equal("Workspace", Assert.Single(workspaces.Value.Items).Name);
        Assert.Equal("owner", Assert.Single(Assert.Single(workspaces.Value.Items).Roles).Code);
    }

    private sealed class TrackingStore : IOrganizationReadStore
    {
        public bool WasCalled { get; private set; }
        public Guid MembershipId { get; private set; }

        public Task<OrganizationReadMember?> GetMemberAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            MembershipId = membershipId;
            return Task.FromResult<OrganizationReadMember?>(CreateMember(membershipId));
        }

        public Task<OrganizationReadPage<OrganizationReadWorkspace>> ListWorkspacesAsync(Guid userId, int limit, string? cursor, CancellationToken cancellationToken)
            => Task.FromResult(new OrganizationReadPage<OrganizationReadWorkspace>([new(Guid.NewGuid(), "Workspace", [new(Guid.NewGuid(), "owner", "Owner")], Guid.NewGuid(), 2, 3)], "workspaces-next"));
        public Task<OrganizationReadPage<OrganizationReadMember>> ListMembersAsync(Guid organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new OrganizationReadPage<OrganizationReadMember>([CreateMember(Guid.NewGuid())], "members-next"));
        }

        private static OrganizationReadMember CreateMember(Guid membershipId)
            => new(membershipId, Guid.NewGuid(), "Reader", "reader@example.test", "active", [new(Guid.NewGuid(), "member", "Member")], DateTimeOffset.UtcNow, 1);
    }

    private sealed class DeniedAuthorization : IOrganizationAuthorization
    {
        public Task<IReadOnlySet<Guid>> GetActiveMemberUserIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Guid MembershipId { get; } = Guid.NewGuid();
        public string? LastCapability { get; private set; }
        public bool AllowGeneralRead { get; init; }
        public Task<OrganizationMemberSecurityContext> AuthorizeMemberScopeAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new OrganizationMemberSecurityContext(organizationId, 1, MembershipId, Guid.NewGuid(), "active", "active", DateTimeOffset.UtcNow, 1, false, false, [], new HashSet<string>()));
        public Task<OrganizationMemberSecurityContext> AuthorizeCapabilityAsync(Guid organizationId, string requiredPermissionCode, CancellationToken cancellationToken = default)
        {
            LastCapability = requiredPermissionCode;
            if (AllowGeneralRead)
            {
                return AuthorizeMemberScopeAsync(organizationId, cancellationToken);
            }

            throw new ForbiddenException("Organization.AccessDenied", "Access denied.");
        }

        public Task<Guid> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<IReadOnlyList<OrganizationWorkspaceBrief>> ListEligibleWorkspacesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrganizationMemberSecurityContext> AuthorizeRoleCatalogReadAsync(Guid organizationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrganizationMemberSecurityContext> ReauthorizeJobActorAsync(Guid organizationId, Guid actorUserId, string? requiredPermissionCode = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrganizationMemberSecurityContext?> LoadMemberSecurityContextAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrganizationMemberSecurityContext?> EvaluateActorAsync(Guid organizationId, Guid userId, Guid? sessionId, string? requiredPermissionCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public bool CanAccessProjectResource(OrganizationMemberSecurityContext memberContext, Guid projectId) => false;
    }
}
