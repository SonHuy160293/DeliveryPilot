using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.Identity.Tests.Persistence;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class OrganizationAuthorizationAndGuardTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuiltInAuthorityUsesOnlyMatchingPersistedAllowedPermissions()
    {
        Guid organizationId = Guid.CreateVersion7();
        Guid otherOrganizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed seed = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, FixedNow);

        Assert.Equal(OrganizationCapabilityCatalog.AllPermissionCodes.Count, seed.Permissions.Count(x => x.RoleId == seed.OwnerRole.Id));
        Assert.Equal(OrganizationCapabilityCatalog.DelegablePermissionCodes.Count, seed.Permissions.Count(x => x.RoleId == seed.AdminRole.Id));
        Assert.Empty(seed.Permissions.Where(x => x.RoleId == seed.MemberRole.Id));

        OrganizationRolePermission[] rows =
        [
            OrganizationRolePermission.Create(organizationId, seed.OwnerRole.Id, OrganizationCapabilityCatalog.MembersRead),
            OrganizationRolePermission.Create(organizationId, seed.OwnerRole.Id, OrganizationCapabilityCatalog.OwnershipManage),
            OrganizationRolePermission.Create(organizationId, seed.OwnerRole.Id, "platform.admin"),
            OrganizationRolePermission.Create(otherOrganizationId, seed.OwnerRole.Id, OrganizationCapabilityCatalog.RolesManage),
            OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.RolesAssign),
            OrganizationRolePermission.Create(organizationId, seed.AdminRole.Id, OrganizationCapabilityCatalog.OwnershipManage),
            OrganizationRolePermission.Create(organizationId, seed.MemberRole.Id, OrganizationCapabilityCatalog.AuditRead),
        ];

        IReadOnlySet<string> owner = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, rows);
        Assert.True(owner.SetEquals([OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.OwnershipManage]));
        Assert.DoesNotContain(OrganizationCapabilityCatalog.AuditRead, owner);
        Assert.True(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.AdminRole, rows).SetEquals([OrganizationCapabilityCatalog.RolesAssign]));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.MemberRole, rows));
        Assert.Empty(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(seed.OwnerRole, []));
        EffectiveOrganizationAuthority authority = OrganizationCapabilityCatalog.ComputeEffectiveAuthority(organizationId, [seed.OwnerRole], []);
        Assert.True(authority.IsSystemOwner);
        Assert.Empty(authority.EffectivePermissions);
    }

    [Fact]
    public void CapabilityCatalogAndBuiltInTemplatesEnforceProtectedOwnerAndDelegationCeiling()
    {
        Guid organizationId = Guid.CreateVersion7();
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organizationId, FixedNow);

        Assert.Equal(3, builtIns.Roles.Count);
        Assert.True(OrganizationCapabilityCatalog.IsSystemOwnerRole(builtIns.OwnerRole));
        Assert.True(OrganizationCapabilityCatalog.IsSystemAdminRole(builtIns.AdminRole));
        Assert.True(OrganizationCapabilityCatalog.IsSystemMemberRole(builtIns.MemberRole));

        Assert.True(OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName("owner"));
        Assert.True(OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName(" Owner "));
        Assert.True(OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName("ADMIN"));
        Assert.True(OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName("member"));
        Assert.False(OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName("audit-reader"));

        Assert.Equal(10, OrganizationCapabilityCatalog.DelegablePermissionCodes.Count);
        Assert.DoesNotContain(OrganizationCapabilityCatalog.OwnershipManage, OrganizationCapabilityCatalog.DelegablePermissionCodes);
        Assert.False(OrganizationCapabilityCatalog.IsDelegablePermission(OrganizationCapabilityCatalog.OwnershipManage));
        Assert.False(OrganizationCapabilityCatalog.IsDelegablePermission("cinema.branch.manage"));
        Assert.False(OrganizationCapabilityCatalog.IsDelegablePermission("platform.operator"));
        Assert.False(OrganizationCapabilityCatalog.IsDelegablePermission("project.content.read"));

        Guid customRoleId = Guid.Parse("019a0000-0000-7000-8000-000000000004");
        Assert.Equal("role-019a0000000070008000000000000004", OrganizationCapabilityCatalog.FormatCustomRoleCode(customRoleId));

        // A custom role named "Owner" or attempting to carry protected/platform codes never confers system Owner authority.
        var fakeOwnerCustomRole = OrganizationRole.Create(
            organizationId,
            OrganizationCapabilityCatalog.FormatCustomRoleCode(customRoleId),
            "Owner",
            OrganizationCapabilityCatalog.CustomOrigin,
            templateVersion: null,
            Guid.CreateVersion7(),
            FixedNow);

        var secondCustomRole = OrganizationRole.Create(
            organizationId,
            OrganizationCapabilityCatalog.FormatCustomRoleCode(Guid.CreateVersion7()),
            "Invite Issuer",
            OrganizationCapabilityCatalog.CustomOrigin,
            templateVersion: null,
            Guid.CreateVersion7(),
            FixedNow);

        OrganizationRolePermission[] customPermissions =
        [
            OrganizationRolePermission.Create(organizationId, fakeOwnerCustomRole.Id, OrganizationCapabilityCatalog.OwnershipManage),
            OrganizationRolePermission.Create(organizationId, fakeOwnerCustomRole.Id, "platform.admin"),
            OrganizationRolePermission.Create(organizationId, fakeOwnerCustomRole.Id, OrganizationCapabilityCatalog.AuditRead),
            OrganizationRolePermission.Create(organizationId, secondCustomRole.Id, OrganizationCapabilityCatalog.InvitationsIssue),
        ];

        EffectiveOrganizationAuthority combinedAuthority = OrganizationCapabilityCatalog.ComputeEffectiveAuthority(
            organizationId,
            [fakeOwnerCustomRole, secondCustomRole],
            customPermissions);

        Assert.False(combinedAuthority.IsSystemOwner);
        Assert.False(combinedAuthority.IsSystemAdmin);
        Assert.DoesNotContain(OrganizationCapabilityCatalog.OwnershipManage, combinedAuthority.EffectivePermissions);
        Assert.DoesNotContain("platform.admin", combinedAuthority.EffectivePermissions);
        Assert.Contains(OrganizationCapabilityCatalog.AuditRead, combinedAuthority.EffectivePermissions);
        Assert.Contains(OrganizationCapabilityCatalog.InvitationsIssue, combinedAuthority.EffectivePermissions);
        Assert.Equal(2, combinedAuthority.EffectivePermissions.Count);
    }

    [Fact]
    public void InvitationOfferRequiresRoleManagementAuthority()
    {
        Guid creatorId = Guid.CreateVersion7();
        var organization = Organization.Create("Workspace", creatorId, FixedNow);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, FixedNow);
        var membership = OrganizationMembership.Create(organization.Id, creatorId, creatorId, FixedNow);
        var permissions = new HashSet<string>(StringComparer.Ordinal) { OrganizationCapabilityCatalog.InvitationsIssue };
        var actor = new OrganizationMemberSecurityContext(organization.Id, organization.Version, membership.Id, creatorId, "active", "active", membership.CreatedAt, membership.Version, false, false, [], permissions);
        var scope = new OrganizationMutationScope(organization, membership, actor, 1);

        Assert.Throws<ForbiddenException>(() => scope.EnsureCanOfferRoles(OrganizationCapabilityCatalog.InvitationsIssue, [builtIns.MemberRole], builtIns.Permissions));

        permissions.Add(OrganizationCapabilityCatalog.RolesManage);
        scope.EnsureCanOfferRoles(OrganizationCapabilityCatalog.InvitationsIssue, [builtIns.MemberRole], builtIns.Permissions);
    }

    [Fact]
    public void AdminOrDelegateCannotDemoteAdminThenRemoveAndTargetCeilingAndMultiRoleOwnerProtectionHold()
    {
        Guid creatorId = Guid.CreateVersion7();
        var organization = Organization.Create("Acme Security", creatorId, FixedNow);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, FixedNow);

        var adminMembership = OrganizationMembership.Create(organization.Id, Guid.CreateVersion7(), creatorId, FixedNow);
        var adminBrief = new OrganizationRoleBrief(builtIns.AdminRole.Id, builtIns.AdminRole.Code, builtIns.AdminRole.Name, builtIns.AdminRole.Origin, 1, 1);
        var ownerBrief = new OrganizationRoleBrief(builtIns.OwnerRole.Id, builtIns.OwnerRole.Code, builtIns.OwnerRole.Name, builtIns.OwnerRole.Origin, 1, 1);
        var memberBrief = new OrganizationRoleBrief(builtIns.MemberRole.Id, builtIns.MemberRole.Code, builtIns.MemberRole.Name, builtIns.MemberRole.Origin, 1, 1);

        var adminActor = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            adminMembership.Id,
            adminMembership.UserId,
            "active",
            "active",
            adminMembership.CreatedAt,
            adminMembership.Version,
            IsSystemOwner: false,
            IsSystemAdmin: true,
            EffectiveRoles: [adminBrief],
            EffectivePermissions: new HashSet<string>(OrganizationCapabilityCatalog.DelegablePermissionCodes, StringComparer.Ordinal));

        var peerAdminContext = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "active",
            "active",
            FixedNow,
            1,
            IsSystemOwner: false,
            IsSystemAdmin: true,
            EffectiveRoles: [adminBrief],
            EffectivePermissions: new HashSet<string>(OrganizationCapabilityCatalog.DelegablePermissionCodes, StringComparer.Ordinal));

        var adminScope = new OrganizationMutationScope(
            organization,
            adminMembership,
            adminActor,
            ActiveEligibleOwnerCount: 1);

        // Admin cannot revoke another Admin's Admin role (anti-bypass: cannot demote Admin then remove them).
        ForbiddenException revokeAdminEx = Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanRevokeOrdinaryRole(builtIns.AdminRole, builtIns.Permissions, peerAdminContext));
        Assert.Equal("Organization.AccessDenied", revokeAdminEx.Code);

        // Admin cannot remove an Admin membership directly.
        ForbiddenException removeAdminEx = Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanRemoveMember(peerAdminContext));
        Assert.Equal("Organization.AccessDenied", removeAdminEx.Code);

        // Multi-role Owner target (holding both Owner and a custom role) is protected from Admin role edits and removal.
        var customRole = OrganizationRole.Create(
            organization.Id,
            OrganizationCapabilityCatalog.FormatCustomRoleCode(Guid.CreateVersion7()),
            "Audit Reader",
            OrganizationCapabilityCatalog.CustomOrigin,
            templateVersion: null,
            creatorId,
            FixedNow);
        var customPerm = OrganizationRolePermission.Create(organization.Id, customRole.Id, OrganizationCapabilityCatalog.AuditRead);
        var customBrief = new OrganizationRoleBrief(customRole.Id, customRole.Code, customRole.Name, customRole.Origin, 1, 1);
        OrganizationRoleBrief[] multiRoleOwnerRoles = [ownerBrief, customBrief];
        var allPerms = new HashSet<string>(OrganizationCapabilityCatalog.AllPermissionCodes, StringComparer.Ordinal);

        var multiRoleOwnerTarget = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            Guid.CreateVersion7(),
            creatorId,
            "active",
            "active",
            FixedNow,
            1,
            IsSystemOwner: true,
            IsSystemAdmin: false,
            EffectiveRoles: multiRoleOwnerRoles,
            EffectivePermissions: allPerms);

        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanRevokeOrdinaryRole(customRole, [customPerm], multiRoleOwnerTarget));
        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanGrantOrdinaryRole(customRole, [customPerm], multiRoleOwnerTarget));
        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanRemoveMember(multiRoleOwnerTarget));

        // Admin cannot install protected Owner through ordinary grant or invitation offer, nor define a custom role with OwnershipManage.
        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanGrantOrdinaryRole(builtIns.OwnerRole, builtIns.Permissions));
        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanOfferRoles(OrganizationCapabilityCatalog.InvitationsIssue, [builtIns.OwnerRole], builtIns.Permissions));
        Assert.Throws<ForbiddenException>(() =>
            adminScope.EnsureCanDefineCustomRole([OrganizationCapabilityCatalog.OwnershipManage]));

        // Delegated remover with only [organization.members.remove] cannot remove a target who holds [organization.audit.read].
        var delegatedRemover = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "active",
            "active",
            FixedNow,
            1,
            IsSystemOwner: false,
            IsSystemAdmin: false,
            EffectiveRoles: [],
            EffectivePermissions: new HashSet<string>([OrganizationCapabilityCatalog.MembersRemove], StringComparer.Ordinal));

        var delegatedScope = new OrganizationMutationScope(
            organization,
            adminMembership,
            delegatedRemover,
            ActiveEligibleOwnerCount: 1);

        var auditHolderTarget = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "active",
            "active",
            FixedNow,
            1,
            IsSystemOwner: false,
            IsSystemAdmin: false,
            EffectiveRoles: [customBrief],
            EffectivePermissions: new HashSet<string>([OrganizationCapabilityCatalog.AuditRead], StringComparer.Ordinal));

        var basicMemberTarget = new OrganizationMemberSecurityContext(
            organization.Id,
            organization.Version,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "active",
            "active",
            FixedNow,
            1,
            IsSystemOwner: false,
            IsSystemAdmin: false,
            EffectiveRoles: [memberBrief],
            EffectivePermissions: new HashSet<string>(StringComparer.Ordinal));

        Assert.Throws<ForbiddenException>(() => delegatedScope.EnsureCanRemoveMember(auditHolderTarget));
        delegatedScope.EnsureCanRemoveMember(basicMemberTarget);
    }

    [PostgreSqlFact]
    public async Task WorkspaceIsolationIgnoresForgedClaimsJobTitlesAndPlatformAssignmentsAndNeverGrantsProjectAccess()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"))
            .Options;

        var userA = User.Create($"authz-a-{Guid.NewGuid():N}@example.com", "Owner Admin", status: "active");
        var userB = User.Create($"authz-b-{Guid.NewGuid():N}@example.com", "Bob", status: "active");

        var orgA = Organization.Create("Workspace A", userA.Id, FixedNow);
        var orgB = Organization.Create("Workspace B", userB.Id, FixedNow);
        OrganizationBuiltInRoleSeed builtInsA = OrganizationCapabilityCatalog.CreateBuiltInRoles(orgA.Id, FixedNow);
        OrganizationBuiltInRoleSeed builtInsB = OrganizationCapabilityCatalog.CreateBuiltInRoles(orgB.Id, FixedNow);

        var membershipA = OrganizationMembership.Create(orgA.Id, userA.Id, userA.Id, FixedNow);
        var membershipB = OrganizationMembership.Create(orgB.Id, userB.Id, userB.Id, FixedNow);

        var ownerGrantA = OrganizationRoleAssignment.Create(orgA.Id, membershipA.Id, builtInsA.OwnerRole.Id, userA.Id, FixedNow);
        var ownerGrantB = OrganizationRoleAssignment.Create(orgB.Id, membershipB.Id, builtInsB.OwnerRole.Id, userB.Id, FixedNow);

        // Even an in-memory PlatformRoleAssignment domain object cannot grant workspace B or project access.
        var platformAssignment = PlatformRoleAssignment.Create(userA.Id, "platform.admin", userA.Id, userB.Id, "AUTH-REF-001", FixedNow, "Bootstrap");
        Assert.Equal("platform.admin", platformAssignment.RoleCode);

        await using var context = new IdentityDbContext(options);
        try
        {
            context.Users.AddRange(userA, userB);
            context.Organizations.AddRange(orgA, orgB);
            context.OrganizationRoles.AddRange(builtInsA.Roles);
            context.OrganizationRoles.AddRange(builtInsB.Roles);
            context.OrganizationRolePermissions.AddRange(builtInsA.Permissions);
            context.OrganizationRolePermissions.AddRange(builtInsB.Permissions);
            context.OrganizationMemberships.AddRange(membershipA, membershipB);
            context.OrganizationRoleAssignments.AddRange(ownerGrantA, ownerGrantB);
            await context.SaveChangesAsync();

            // Forged claims pretend userA has TenantIds=[orgB.Id] and full permissions.
            var forgedCurrentUser = new StubCurrentUserProvider(new CurrentUser(
                Id: userA.Id.ToString(),
                UserName: "Owner Admin",
                Email: userA.PrimaryEmail,
                TenantIds: [orgB.Id.ToString()],
                BranchIds: [orgB.Id.ToString()],
                Permissions: [OrganizationCapabilityCatalog.MembersRead, OrganizationCapabilityCatalog.OwnershipManage, "project.read"],
                SessionId: null));

            var clock = new FixedTimeProvider(FixedNow);
            var authz = new OrganizationAuthorizationService(context, forgedCurrentUser, clock);
            var guard = new OrganizationMutationGuard(context, forgedCurrentUser, clock);
            var uow = new IdentityUnitOfWork(context);

            IReadOnlyList<OrganizationWorkspaceBrief> workspaces = await authz.ListEligibleWorkspacesAsync();
            OrganizationWorkspaceBrief singleWorkspace = Assert.Single(workspaces);
            Assert.Equal(orgA.Id, singleWorkspace.OrganizationId);

            OrganizationMemberSecurityContext contextA = await authz.AuthorizeMemberScopeAsync(orgA.Id);
            Assert.True(contextA.IsSystemOwner);
            Assert.False(authz.CanAccessProjectResource(contextA, Guid.CreateVersion7()));

            ForbiddenException crossScopeRead = await Assert.ThrowsAsync<ForbiddenException>(() =>
                authz.AuthorizeMemberScopeAsync(orgB.Id));
            Assert.Equal("Organization.AccessDenied", crossScopeRead.Code);

            ForbiddenException crossScopeCapability = await Assert.ThrowsAsync<ForbiddenException>(() =>
                authz.AuthorizeCapabilityAsync(orgB.Id, OrganizationCapabilityCatalog.MembersRead));
            Assert.Equal("Organization.AccessDenied", crossScopeCapability.Code);

            ForbiddenException crossScopeGuard = await Assert.ThrowsAsync<ForbiddenException>(() =>
                uow.ExecuteInTransactionAsync(ct => guard.AcquireAsync(orgB.Id, membershipB.Id, ct)));
            Assert.Equal("Organization.AccessDenied", crossScopeGuard.Code);

            int seededOwnerPermissions = await context.OrganizationRolePermissions.CountAsync(p => p.OrganizationId == orgA.Id && p.RoleId == builtInsA.OwnerRole.Id);
            Assert.Equal(OrganizationCapabilityCatalog.AllPermissionCodes.Count, seededOwnerPermissions);
            await context.OrganizationRolePermissions.Where(p => p.OrganizationId == orgA.Id
                && p.RoleId == builtInsA.OwnerRole.Id && p.PermissionCode == OrganizationCapabilityCatalog.AuditRead).ExecuteDeleteAsync();
            Assert.True((await authz.AuthorizeMemberScopeAsync(orgA.Id)).IsSystemOwner);
            await Assert.ThrowsAsync<ForbiddenException>(() => authz.AuthorizeCapabilityAsync(orgA.Id, OrganizationCapabilityCatalog.AuditRead));
            OrganizationMutationScope freshGuard = await uow.ExecuteInTransactionAsync(ct => guard.AcquireAsync(orgA.Id, null, ct));
            Assert.True(freshGuard.Actor.IsSystemOwner);
            Assert.False(freshGuard.Actor.HasPermission(OrganizationCapabilityCatalog.AuditRead));
            Assert.True(freshGuard.Actor.HasPermission(OrganizationCapabilityCatalog.MembersRead));
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.OrganizationRoleAssignments.Where(x => x.OrganizationId == orgA.Id || x.OrganizationId == orgB.Id).ExecuteDeleteAsync();
            await context.OrganizationRolePermissions.Where(x => x.OrganizationId == orgA.Id || x.OrganizationId == orgB.Id).ExecuteDeleteAsync();
            await context.OrganizationRoles.Where(x => x.OrganizationId == orgA.Id || x.OrganizationId == orgB.Id).ExecuteDeleteAsync();
            await context.OrganizationMemberships.Where(x => x.OrganizationId == orgA.Id || x.OrganizationId == orgB.Id).ExecuteDeleteAsync();
            await context.Organizations.Where(x => x.Id == orgA.Id || x.Id == orgB.Id).ExecuteDeleteAsync();
            await context.Users.Where(x => x.Id == userA.Id || x.Id == userB.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task CompetingLastOwnerReductionsSerializeAndPreserveAtLeastOneActiveEligibleOwnerWithoutRequiringSessions()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"))
            .Options;

        var owner1 = User.Create($"owner1-{Guid.NewGuid():N}@example.com", "Owner One", status: "active");
        var owner2 = User.Create($"owner2-{Guid.NewGuid():N}@example.com", "Owner Two", status: "active");
        var adminUser = User.Create($"admin-{Guid.NewGuid():N}@example.com", "Admin User", status: "active");

        var organization = Organization.Create("Continuity Org", owner1.Id, FixedNow);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, FixedNow);

        var membership1 = OrganizationMembership.Create(organization.Id, owner1.Id, owner1.Id, FixedNow);
        var membership2 = OrganizationMembership.Create(organization.Id, owner2.Id, owner1.Id, FixedNow);
        var adminMembership = OrganizationMembership.Create(organization.Id, adminUser.Id, owner1.Id, FixedNow);

        var ownerGrant1 = OrganizationRoleAssignment.Create(organization.Id, membership1.Id, builtIns.OwnerRole.Id, owner1.Id, FixedNow);
        var ownerGrant2 = OrganizationRoleAssignment.Create(organization.Id, membership2.Id, builtIns.OwnerRole.Id, owner1.Id, FixedNow);
        var adminGrant = OrganizationRoleAssignment.Create(organization.Id, adminMembership.Id, builtIns.AdminRole.Id, owner1.Id, FixedNow);

        await using var setupContext = new IdentityDbContext(options);
        try
        {
            setupContext.Users.AddRange(owner1, owner2, adminUser);
            setupContext.Organizations.Add(organization);
            setupContext.OrganizationRoles.AddRange(builtIns.Roles);
            setupContext.OrganizationRolePermissions.AddRange(builtIns.Permissions);
            setupContext.OrganizationMemberships.AddRange(membership1, membership2, adminMembership);
            setupContext.OrganizationRoleAssignments.AddRange(ownerGrant1, ownerGrant2, adminGrant);
            await setupContext.SaveChangesAsync();

            using var barrier = new Barrier(2);
            var clock = new FixedTimeProvider(FixedNow.AddMinutes(5));

            async Task<string> AttemptOwnerStepDownAsync(Guid actorUserId, Guid targetMembershipId)
            {
                await using var txContext = new IdentityDbContext(options);
                var uow = new IdentityUnitOfWork(txContext);
                var guard = new OrganizationMutationGuard(
                    txContext,
                    new StubCurrentUserProvider(new CurrentUser(actorUserId.ToString(), null, null, [], [], [], null)),
                    clock);

                barrier.SignalAndWait(TimeSpan.FromSeconds(10));

                try
                {
                    await uow.ExecuteInTransactionAsync(async ct =>
                    {
                        OrganizationMutationScope scope = await guard.AcquireAsync(organization.Id, targetMembershipId, ct);
                        Assert.NotNull(scope.Target);
                        scope.EnsureCanDemoteOwner(scope.Target!);

                        OrganizationRoleAssignment activeOwnerAssignment = await txContext.OrganizationRoleAssignments
                            .Where(a => a.OrganizationId == organization.Id
                                && a.MembershipId == targetMembershipId
                                && a.RoleId == builtIns.OwnerRole.Id)
                            .SingleAsync(ct);

                        activeOwnerAssignment.Revoke(actorUserId, clock.GetUtcNow(), "Stepping down");
                        txContext.OrganizationRoleAssignments.Add(
                            OrganizationRoleAssignment.Create(organization.Id, targetMembershipId, builtIns.MemberRole.Id, actorUserId, clock.GetUtcNow(), null, "Member fallback"));
                        scope.TargetMembership!.RecordAssignmentChange(clock.GetUtcNow());
                        return true;
                    });
                    return "committed";
                }
                catch (ConflictException ex) when (ex.Code == OrganizationMutationScope.OwnerContinuityErrorCode)
                {
                    return "blocked-continuity";
                }
            }

            string[] outcomes = await Task.WhenAll(
                AttemptOwnerStepDownAsync(owner1.Id, membership1.Id),
                AttemptOwnerStepDownAsync(owner2.Id, membership2.Id));

            Assert.Equal(1, outcomes.Count(x => x == "committed"));
            Assert.Equal(1, outcomes.Count(x => x == "blocked-continuity"));

            // Verify exactly one active Owner assignment remains even though neither Owner had an AuthSession.
            int remainingOwners = await setupContext.OrganizationRoleAssignments
                .AsNoTracking()
                .CountAsync(a => a.OrganizationId == organization.Id
                    && a.RoleId == builtIns.OwnerRole.Id
                    && a.RevokedAt == null);
            Assert.Equal(1, remainingOwners);

            // Now suspend the remaining active Owner's User account (compromised/suspended containment).
            Guid survivingOwnerMembershipId = await setupContext.OrganizationRoleAssignments
                .AsNoTracking()
                .Where(a => a.OrganizationId == organization.Id && a.RoleId == builtIns.OwnerRole.Id && a.RevokedAt == null)
                .Select(a => a.MembershipId)
                .SingleAsync();
            Guid survivingOwnerUserId = survivingOwnerMembershipId == membership1.Id ? owner1.Id : owner2.Id;

            await setupContext.Users
                .Where(u => u.Id == survivingOwnerUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "suspended"));

            // Compromised/suspended Owner is immediately denied.
            var suspendedOwnerAuthz = new OrganizationAuthorizationService(
                setupContext,
                new StubCurrentUserProvider(new CurrentUser(survivingOwnerUserId.ToString(), null, null, [], [], [], null)),
                clock);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                suspendedOwnerAuthz.AuthorizeMemberScopeAsync(organization.Id));

            // Active Admin can still perform ordinary scoped reads, but privileged mutations are blocked with no auto-promotion.
            var adminCurrentUser = new StubCurrentUserProvider(new CurrentUser(adminUser.Id.ToString(), null, null, [], [], [], null));
            var adminAuthz = new OrganizationAuthorizationService(setupContext, adminCurrentUser, clock);
            OrganizationMemberSecurityContext adminReadContext = await adminAuthz.AuthorizeCapabilityAsync(
                organization.Id,
                OrganizationCapabilityCatalog.MembersRead);
            Assert.True(adminReadContext.IsSystemAdmin);
            Assert.False(adminReadContext.IsSystemOwner);

            await using var adminTxContext = new IdentityDbContext(options);
            var adminUow = new IdentityUnitOfWork(adminTxContext);
            var adminGuard = new OrganizationMutationGuard(adminTxContext, adminCurrentUser, clock);

            ConflictException noOwnerMutationConflict = await Assert.ThrowsAsync<ConflictException>(() =>
                adminUow.ExecuteInTransactionAsync(async ct =>
                {
                    OrganizationMutationScope scope = await adminGuard.AcquireAsync(organization.Id, null, ct);
                    Assert.Equal(0, scope.ActiveEligibleOwnerCount);
                    scope.EnsureCanDefineCustomRole([OrganizationCapabilityCatalog.MembersRead]);
                    return true;
                }));
            Assert.Equal(OrganizationMutationScope.OwnerContinuityErrorCode, noOwnerMutationConflict.Code);
        }
        finally
        {
            setupContext.ChangeTracker.Clear();
            await setupContext.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setupContext.OrganizationRolePermissions.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setupContext.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setupContext.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await setupContext.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await setupContext.Users.Where(x => x.Id == owner1.Id || x.Id == owner2.Id || x.Id == adminUser.Id).ExecuteDeleteAsync();
        }
    }

    [PostgreSqlFact]
    public async Task RevocationBoundariesAndAuthoritativeStoreOutageFailClosedAndFreshDefinitionsOverrideStaleProjections()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("DELIVERYPILOT_TEST_POSTGRES"))
            .Options;

        var owner = User.Create($"owner-rev-{Guid.NewGuid():N}@example.com", "Owner", status: "active");
        var delegateUser = User.Create($"delegate-rev-{Guid.NewGuid():N}@example.com", "Delegate", status: "active");

        var organization = Organization.Create("Revocation Org", owner.Id, FixedNow);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, FixedNow);

        var customRoleId = Guid.CreateVersion7();
        var customRole = OrganizationRole.Create(
            organization.Id,
            OrganizationCapabilityCatalog.FormatCustomRoleCode(customRoleId),
            "Delegated Manager",
            OrganizationCapabilityCatalog.CustomOrigin,
            templateVersion: null,
            owner.Id,
            FixedNow);

        var permManage = OrganizationRolePermission.Create(organization.Id, customRole.Id, OrganizationCapabilityCatalog.RolesManage);
        var permAudit = OrganizationRolePermission.Create(organization.Id, customRole.Id, OrganizationCapabilityCatalog.AuditRead);

        var ownerMembership = OrganizationMembership.Create(organization.Id, owner.Id, owner.Id, FixedNow);
        var delegateMembership = OrganizationMembership.Create(organization.Id, delegateUser.Id, owner.Id, FixedNow);

        var ownerGrant = OrganizationRoleAssignment.Create(organization.Id, ownerMembership.Id, builtIns.OwnerRole.Id, owner.Id, FixedNow);
        var delegateGrant = OrganizationRoleAssignment.Create(organization.Id, delegateMembership.Id, customRole.Id, owner.Id, FixedNow);

        await using var context = new IdentityDbContext(options);
        try
        {
            context.Users.AddRange(owner, delegateUser);
            context.Organizations.Add(organization);
            context.OrganizationRoles.AddRange(builtIns.Roles);
            context.OrganizationRoles.Add(customRole);
            context.OrganizationRolePermissions.AddRange(builtIns.Permissions);
            context.OrganizationRolePermissions.AddRange(permManage, permAudit);
            context.OrganizationMemberships.AddRange(ownerMembership, delegateMembership);
            context.OrganizationRoleAssignments.AddRange(ownerGrant, delegateGrant);
            await context.SaveChangesAsync();

            var clock = new FixedTimeProvider(FixedNow.AddMinutes(10));
            var staleClaimPrincipal = new StubCurrentUserProvider(new CurrentUser(
                Id: delegateUser.Id.ToString(),
                UserName: "Delegate",
                Email: delegateUser.PrimaryEmail,
                TenantIds: [organization.Id.ToString()],
                BranchIds: [],
                Permissions: [OrganizationCapabilityCatalog.RolesManage, OrganizationCapabilityCatalog.AuditRead],
                SessionId: null));

            var authz = new OrganizationAuthorizationService(context, staleClaimPrincipal, clock);

            // Admitted read before revocation succeeds and retains its already-loaded snapshot.
            OrganizationMemberSecurityContext admittedReadSnapshot = await authz.AuthorizeCapabilityAsync(
                organization.Id,
                OrganizationCapabilityCatalog.AuditRead);
            Assert.True(admittedReadSnapshot.HasPermission(OrganizationCapabilityCatalog.AuditRead));

            // Remove AuditRead from the custom role definition in DB; fresh definitions override stale claims/projections.
            await context.OrganizationRolePermissions
                .Where(p => p.OrganizationId == organization.Id
                    && p.RoleId == customRole.Id
                    && p.PermissionCode == OrganizationCapabilityCatalog.AuditRead)
                .ExecuteDeleteAsync();

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                authz.AuthorizeCapabilityAsync(organization.Id, OrganizationCapabilityCatalog.AuditRead));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                authz.ReauthorizeJobActorAsync(organization.Id, delegateUser.Id, OrganizationCapabilityCatalog.AuditRead));

            // Now remove the delegate's membership; subsequent mutation guard and job reauthorization fail closed.
            await context.OrganizationMemberships
                .Where(m => m.Id == delegateMembership.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "removed"));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                authz.ReauthorizeJobActorAsync(organization.Id, delegateUser.Id, OrganizationCapabilityCatalog.RolesManage));

            await using var txContext = new IdentityDbContext(options);
            var uow = new IdentityUnitOfWork(txContext);
            var guard = new OrganizationMutationGuard(txContext, staleClaimPrincipal, clock);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                uow.ExecuteInTransactionAsync(ct => guard.AcquireAsync(organization.Id, null, ct)));

            // Authoritative store outage fails closed with 503 ServiceUnavailableException rather than accepting claims.
            var unavailableOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=postgres;Password=invalid;Timeout=1")
                .Options;
            await using var unavailableContext = new IdentityDbContext(unavailableOptions);
            var outageAuthz = new OrganizationAuthorizationService(unavailableContext, staleClaimPrincipal, clock);

            ServiceUnavailableException outageEx = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
                outageAuthz.AuthorizeCapabilityAsync(organization.Id, OrganizationCapabilityCatalog.RolesManage));
            Assert.Equal(OrganizationAuthorizationService.AuthorityUnavailableErrorCode, outageEx.Code);
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.OrganizationRoleAssignments.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationRolePermissions.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationRoles.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.OrganizationMemberships.Where(x => x.OrganizationId == organization.Id).ExecuteDeleteAsync();
            await context.Organizations.Where(x => x.Id == organization.Id).ExecuteDeleteAsync();
            await context.Users.Where(x => x.Id == owner.Id || x.Id == delegateUser.Id).ExecuteDeleteAsync();
        }
    }

    private sealed class StubCurrentUserProvider(CurrentUser? currentUser) : ICurrentUserProvider
    {
        public CurrentUser? GetCurrentUser() => currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
