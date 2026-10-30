using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations;

public sealed class OwnershipNominationService(
    IOrganizationMutationGuard guard,
    IOwnershipNominationStore store,
    IOrganizationMembershipStore memberships,
    IIdentityAuditWriter audit,
    TimeProvider clock)
{
    public async Task<NominationSummary> NominateAsync(Guid organizationId, Guid membershipId, string correlationId, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, membershipId, cancellationToken);
        scope.EnsureOwnerAuthority();
        scope.EnsurePrivilegedChangeAllowed();
        OrganizationMembership target = scope.TargetMembership ?? throw Denied();
        if (scope.Target?.IsEligibleActiveMember != true)
        {
            throw Conflict("RecipientUnavailable", "The nominated membership is not eligible.");
        }

        if (scope.Target.IsSystemOwner)
        {
            throw Conflict("AlreadyOwner", "The member already holds ownership.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        string correlation = SafeCorrelation(correlationId);
        OrganizationOwnershipNomination? previous = await store.FindPendingAsync(organizationId, membershipId, cancellationToken);
        if (previous is not null)
        {
            if (previous.ExpiresAt > now)
            {
                throw Conflict("PendingExists", "A pending ownership nomination already exists.");
            }

            await store.CloseExpiredAsync(previous, now, cancellationToken);
            await audit.StageAsync(IdentityAuditEntry.ForOwnershipNomination(previous, "pending", IdentityAuditActor.System("nomination-expiry"), now, correlation), cancellationToken);
        }

        var nomination = OrganizationOwnershipNomination.Create(organizationId, target.Id, scope.Actor.UserId, now);
        store.Add(nomination);
        await audit.StageAsync(IdentityAuditEntry.ForOwnershipNomination(nomination, null, IdentityAuditActor.User(scope.Actor.UserId), now, correlation), cancellationToken);
        return Summary(nomination, false);
    }

    public async Task<NominationSummary> RespondAsync(Guid organizationId, Guid nominationId, string action, long version, string? reason, string correlationId, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireForNominationAsync(organizationId, nominationId, cancellationToken);
        OrganizationOwnershipNomination nomination = await store.FindAsync(organizationId, nominationId, cancellationToken) ?? throw Denied();
        bool cancel = action == "cancelled";
        if (cancel)
        {
            scope.EnsureOwnerAuthority();
            scope.EnsurePrivilegedChangeAllowed();
        }
        else if (scope.TargetMembership?.UserId != scope.Actor.UserId || nomination.MembershipId != scope.ActorMembership.Id)
        {
            throw Denied();
        }

        if (nomination.Status == action && nomination.ClosedByUserId == scope.Actor.UserId)
        {
            if (action == "accepted" && !scope.Actor.IsSystemOwner)
            {
                throw Denied();
            }

            return Summary(nomination, false);
        }

        DateTimeOffset now = clock.GetUtcNow();
        if (nomination.Status != "pending" || nomination.ExpiresAt <= now)
        {
            throw Conflict("Closed", "The ownership nomination is closed or expired.");
        }

        if (nomination.Version != version)
        {
            throw Conflict("Stale", "The nomination has changed; refresh before responding.");
        }

        var actor = IdentityAuditActor.User(scope.Actor.UserId);
        string correlation = SafeCorrelation(correlationId);
        OrganizationMembership? membership = null;
        if (action == "accepted")
        {
            if (scope.Sponsor?.Actor.IsEligibleActiveMember != true || !scope.Sponsor.Actor.IsSystemOwner)
            {
                throw Conflict("SponsorUnavailable", "The nomination sponsor is no longer eligible.");
            }

            scope.EnsurePrivilegedChangeAllowed();
            if (scope.Actor.IsSystemOwner)
            {
                throw Conflict("AlreadyOwner", "The member already holds ownership.");
            }

            membership = scope.ActorMembership;
            OrganizationMembershipRoleState state = await memberships.LoadRolesAsync(organizationId, membership.Id, cancellationToken);
            OrganizationRole owner = state.Roles.SingleOrDefault(role => role.RetiredAt is null && OrganizationCapabilityCatalog.IsSystemOwnerRole(role))
                ?? throw new ServiceUnavailableException("Organization.AuthorityUnavailable", "Workspace authorization state is currently unavailable.");
            OrganizationRoleAssignment? assignment = state.Assignments.SingleOrDefault(item => item.RoleId == owner.Id);
            OrganizationRoleAssignmentAuditSnapshot? before = assignment is null ? null : OrganizationRoleAssignmentAuditSnapshot.Capture(assignment);
            if (assignment is null)
            {
                assignment = OrganizationRoleAssignment.Create(organizationId, membership.Id, owner.Id, nomination.NominatedByUserId, now);
                memberships.AddAssignment(assignment);
            }
            else
            {
                assignment.Regrant(nomination.NominatedByUserId, now);
            }

            membership.RecordAssignmentChange(now);
            IdentityAuditEntry grant = before is null
                ? IdentityAuditEntry.ForRoleAssignmentGranted(assignment, membership.Version, owner.PermissionSetVersion, actor, now, correlation)
                : IdentityAuditEntry.ForRoleAssignmentRegranted(before, assignment, membership.Version, owner.PermissionSetVersion, actor, now, correlation);
            await audit.StageAsync(grant, cancellationToken);
        }

        nomination.Close(action, scope.Actor.UserId, now, reason);
        await audit.StageAsync(IdentityAuditEntry.ForOwnershipNomination(nomination, "pending", actor, now, correlation, membership?.Version), cancellationToken);
        return Summary(nomination, false);
    }

    public static NominationSummary Summary(OrganizationOwnershipNomination nomination, bool canAccept)
        => new(nomination.Id, nomination.OrganizationId, nomination.MembershipId, nomination.NominatedByUserId,
            nomination.Status, nomination.CreatedAt, nomination.ExpiresAt, nomination.Version, canAccept);

    private static ForbiddenException Denied() => OrganizationMutationScope.CreateAccessDenied();
    private static ConflictException Conflict(string code, string message) => new($"OwnershipNomination.{code}", message);
    private static string SafeCorrelation(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 100 ? Guid.NewGuid().ToString("D") : value.Trim();
}
