using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships;

public sealed class MembershipChangeService(
    IOrganizationMutationGuard guard,
    IOrganizationMembershipStore store,
    IIdentityAuditWriter audit,
    TimeProvider clock)
{
    public async Task<MembershipChangeResponse> RemoveAsync(Guid organizationId, Guid membershipId, long version, string reason, string correlationId, CancellationToken cancellationToken)
    {
        MembershipChangeValidation.Validate(organizationId, membershipId, version, reason);
        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, membershipId, cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.MembersRemove);
        OrganizationMembership membership = FindTarget(scope);
        scope.EnsureCanRemoveMember(scope.Target!);
        if (membership.Status == "removed")
        {
            scope.EnsureCapability(OrganizationCapabilityCatalog.MembersRead);
            return new MembershipChangeResponse(membership.Id, membership.Status, [], membership.Version, false);
        }

        EnsureVersion(membership, version);
        EnsureActive(scope, membership);
        OrganizationMembershipRoleState state = await store.LoadRolesAsync(organizationId, membershipId, cancellationToken);
        var before = OrganizationMembershipAuditSnapshot.Capture(membership);
        Dictionary<Guid, OrganizationRoleAssignmentAuditSnapshot> grants = state.Assignments.Where(assignment => assignment.RevokedAt is null)
            .ToDictionary(assignment => assignment.Id, OrganizationRoleAssignmentAuditSnapshot.Capture);
        DateTimeOffset now = clock.GetUtcNow();
        string safeReason = reason.Trim();
        string correlation = SafeCorrelation(correlationId);
        var actor = IdentityAuditActor.User(scope.Actor.UserId);
        membership.Remove(scope.Actor.UserId, safeReason, now, state.Assignments);
        await audit.StageAsync(IdentityAuditEntry.ForMembershipStatusChanged(before, membership, actor, now, correlation, safeReason), cancellationToken);
        foreach (OrganizationRoleAssignment assignment in state.Assignments.Where(assignment => grants.ContainsKey(assignment.Id)).OrderBy(assignment => assignment.Id))
        {
            bool protectedRole = state.Roles.Any(role => role.Id == assignment.RoleId &&
                (OrganizationCapabilityCatalog.IsSystemOwnerRole(role) || OrganizationCapabilityCatalog.IsSystemAdminRole(role)));
            await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentRevoked(grants[assignment.Id], assignment, membership.Version, actor, now, correlation, safeReason, protectedRole), cancellationToken);
        }

        return new MembershipChangeResponse(membership.Id, membership.Status, [], membership.Version, true);
    }

    public async Task<MembershipChangeResponse> DemoteOwnerAsync(Guid organizationId, Guid membershipId, long version, string reason, string correlationId, CancellationToken cancellationToken)
    {
        MembershipChangeValidation.Validate(organizationId, membershipId, version, reason);
        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, membershipId, cancellationToken);
        scope.EnsureOwnerAuthority();
        scope.EnsurePrivilegedChangeAllowed();
        OrganizationMembership membership = FindTarget(scope);
        if (scope.TargetRoles?.HasOwnerGrant != true)
        {
            throw InvalidTransition(scope);
        }

        scope.EnsureCanDemoteOwner(scope.Target!);
        EnsureVersion(membership, version);
        EnsureActive(scope, membership);
        OrganizationMembershipRoleState state = await store.LoadRolesAsync(organizationId, membershipId, cancellationToken);
        OrganizationRole ownerRole = state.Roles.Single(role => OrganizationCapabilityCatalog.IsSystemOwnerRole(role) && role.RetiredAt is null);
        OrganizationRole memberRole = state.Roles.SingleOrDefault(role => OrganizationCapabilityCatalog.IsSystemMemberRole(role) && role.RetiredAt is null)
            ?? throw new ServiceUnavailableException("Organization.AuthorityUnavailable", "Workspace authorization is temporarily unavailable.");
        OrganizationRoleAssignment ownerAssignment = state.Assignments.Single(assignment => assignment.RoleId == ownerRole.Id);
        var ownerBefore = OrganizationRoleAssignmentAuditSnapshot.Capture(ownerAssignment);
        OrganizationRoleAssignment? memberAssignment = state.Assignments.SingleOrDefault(assignment => assignment.RoleId == memberRole.Id);
        OrganizationRoleAssignmentAuditSnapshot? memberBefore = memberAssignment is null ? null : OrganizationRoleAssignmentAuditSnapshot.Capture(memberAssignment);
        DateTimeOffset now = clock.GetUtcNow();
        string safeReason = reason.Trim();
        string correlation = SafeCorrelation(correlationId);
        var actor = IdentityAuditActor.User(scope.Actor.UserId);
        OrganizationRoleAssignment? fallback = membership.DemoteOwner(scope.Actor.UserId, safeReason, now, state.Roles, state.Assignments);
        await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentRevoked(ownerBefore, ownerAssignment, membership.Version, actor, now, correlation, safeReason, true), cancellationToken);
        if (fallback is not null)
        {
            if (memberBefore is null)
            {
                store.AddAssignment(fallback);
                await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentGranted(fallback, membership.Version, memberRole.PermissionSetVersion, actor, now, correlation, safeReason), cancellationToken);
            }
            else
            {
                await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentRegranted(memberBefore, fallback, membership.Version, memberRole.PermissionSetVersion, actor, now, correlation, safeReason), cancellationToken);
            }
        }

        IEnumerable<OrganizationRoleAssignment> assignments = fallback is not null && memberBefore is null ? state.Assignments.Append(fallback) : state.Assignments;
        HashSet<Guid> roleIds = assignments.Where(assignment => assignment.RevokedAt is null && (assignment.ExpiresAt is null || assignment.ExpiresAt > now))
            .Select(assignment => assignment.RoleId).ToHashSet();
        OrganizationRoleResponse[] roles = state.Roles.Where(role => roleIds.Contains(role.Id) && role.RetiredAt is null).OrderBy(role => role.Id)
            .Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray();
        return new MembershipChangeResponse(membership.Id, membership.Status, roles, membership.Version, true);
    }

    private static OrganizationMembership FindTarget(OrganizationMutationScope scope)
        => scope.TargetMembership ?? throw (scope.Actor.HasPermission(OrganizationCapabilityCatalog.MembersRead)
            ? new NotFoundException("Membership.NotFound", "Membership was not found.")
            : OrganizationMutationScope.CreateAccessDenied());

    private static void EnsureVersion(OrganizationMembership membership, long version)
    {
        if (membership.Version != version)
        {
            throw new ConflictException("Organization.MembershipStale", "The membership has changed; refresh before editing.");
        }
    }

    private static void EnsureActive(OrganizationMutationScope scope, OrganizationMembership membership)
    {
        if (membership.Status != "active")
        {
            throw InvalidTransition(scope);
        }
    }

    private static Exception InvalidTransition(OrganizationMutationScope scope)
        => scope.Actor.HasPermission(OrganizationCapabilityCatalog.MembersRead)
            ? new ConflictException("Organization.MembershipInvalidTransition", "The membership cannot make this transition.")
            : OrganizationMutationScope.CreateAccessDenied();

    private static string SafeCorrelation(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 100 ? Guid.NewGuid().ToString("D") : value.Trim();
}
