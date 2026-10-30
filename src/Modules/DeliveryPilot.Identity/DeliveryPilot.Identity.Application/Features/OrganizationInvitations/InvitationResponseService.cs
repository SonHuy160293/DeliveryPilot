using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations;

public sealed class InvitationResponseService(
    IInvitationRecipientStore store,
    IInvitationProofService proofs,
    IIdentityAuditWriter audit,
    TimeProvider clock)
{
    public async Task<InvitationPreview> PreviewAsync(Guid invitationId, string? proof, CancellationToken cancellationToken)
    {
        InvitationRecipientContext context = Authorize(await store.ReadAsync(invitationId, cancellationToken), proof);
        EnsurePending(context.Invitation, clock.GetUtcNow());
        bool changed = OfferChanged(context);
        return new InvitationPreview(
            context.Invitation.Id,
            context.Organization.Id,
            context.Organization.Name,
            context.Offers.OrderBy(offer => offer.RoleId).Select(offer => new InvitationRoleSummary(offer.RoleId, offer.OfferedRoleName, offer.OfferedPermissionSetVersion)).ToArray(),
            context.Invitation.ExpiresAt,
            context.Invitation.Version,
            context.Invitation.Status,
            !changed && context.Membership?.Status != "active" && context.Membership?.Status != "suspended",
            changed);
    }

    public async Task<InvitationAcceptance> AcceptAsync(Guid invitationId, string? proof, long expectedVersion, string correlationId, CancellationToken cancellationToken)
    {
        InvitationRecipientContext context = Authorize(await store.AcquireAsync(invitationId, cancellationToken), proof);
        OrganizationInvitation invitation = context.Invitation;
        DateTimeOffset now = clock.GetUtcNow();
        if (invitation.Status == "accepted")
        {
            if (invitation.ClosedByUserId != context.Recipient.Id || context.Membership?.Status != "active")
            {
                throw Unavailable();
            }

            HashSet<Guid> activeRoleIds = context.Assignments.Where(assignment => assignment.MembershipId == context.Membership.Id &&
                assignment.RevokedAt is null && (assignment.ExpiresAt is null || assignment.ExpiresAt > now)).Select(assignment => assignment.RoleId).ToHashSet();
            return Accepted("alreadyAccepted", context.Membership, context.Roles.Where(role => activeRoleIds.Contains(role.Id) && role.RetiredAt is null));
        }

        EnsurePending(invitation, now);
        EnsureVersion(invitation, expectedVersion);
        if (context.Membership?.Status is "active" or "suspended")
        {
            throw new ConflictException("Invitation.RecipientUnavailable", "The invitation cannot be accepted.");
        }

        if (OfferChanged(context))
        {
            throw new ConflictException("Invitation.OfferChanged", "The invitation must be reissued.");
        }

        IdentityAuditActor actor = IdentityAuditActor.User(context.Recipient.Id);
        string correlation = SafeCorrelation(correlationId);
        OrganizationMembership membership;
        OrganizationRoleAssignment[] assignments = context.Assignments.Where(assignment => assignment.MembershipId == context.Membership?.Id).ToArray();
        if (context.Membership is null)
        {
            membership = OrganizationMembership.Create(context.Organization.Id, context.Recipient.Id, context.Recipient.Id, now);
            store.AddMembership(membership);
            await audit.StageAsync(IdentityAuditEntry.ForMembershipCreated(membership, actor, now, correlation), cancellationToken);
        }
        else
        {
            membership = context.Membership;
            OrganizationMembershipAuditSnapshot before = OrganizationMembershipAuditSnapshot.Capture(membership);
            Dictionary<Guid, OrganizationRoleAssignmentAuditSnapshot> historical = assignments.Where(assignment => assignment.RevokedAt is null)
                .ToDictionary(assignment => assignment.Id, OrganizationRoleAssignmentAuditSnapshot.Capture);
            membership.Rejoin(context.Recipient.Id, now, assignments);
            await audit.StageAsync(IdentityAuditEntry.ForMembershipStatusChanged(before, membership, actor, now, correlation), cancellationToken);
            foreach (OrganizationRoleAssignment assignment in assignments.Where(assignment => historical.ContainsKey(assignment.Id)))
            {
                await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentRevoked(
                    historical[assignment.Id],
                    assignment,
                    membership.Version,
                    actor,
                    now,
                    correlation,
                    "Historical grant closed before invitation rejoin."), cancellationToken);
            }
        }

        List<OrganizationRole> offeredRoles = context.Roles.Where(role => context.Offers.Any(offer => offer.RoleId == role.Id)).OrderBy(role => role.Id).ToList();
        foreach (OrganizationRole role in offeredRoles)
        {
            OrganizationRoleAssignment? assignment = assignments.SingleOrDefault(item => item.RoleId == role.Id);
            membership.RecordAssignmentChange(now);
            if (assignment is null)
            {
                assignment = OrganizationRoleAssignment.Create(context.Organization.Id, membership.Id, role.Id, invitation.IssuedByUserId, now);
                store.AddAssignment(assignment);
                await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentGranted(assignment, membership.Version, role.PermissionSetVersion, actor, now, correlation), cancellationToken);
            }
            else
            {
                OrganizationRoleAssignmentAuditSnapshot before = OrganizationRoleAssignmentAuditSnapshot.Capture(assignment);
                assignment.Regrant(invitation.IssuedByUserId, now);
                await audit.StageAsync(IdentityAuditEntry.ForRoleAssignmentRegranted(before, assignment, membership.Version, role.PermissionSetVersion, actor, now, correlation), cancellationToken);
            }
        }

        invitation.Close("accepted", context.Recipient.Id, now);
        await audit.StageAsync(IdentityAuditEntry.ForInvitationClosed(
            invitation.OrganizationId,
            invitation.Id,
            invitation.Version,
            "pending",
            "accepted",
            context.Recipient.Id,
            now,
            actor,
            now,
            correlation,
            membershipId: membership.Id,
            membershipVersion: membership.Version), cancellationToken);
        return Accepted("accepted", membership, offeredRoles);
    }

    public async Task<InvitationDecline> DeclineAsync(Guid invitationId, string? proof, long expectedVersion, string correlationId, CancellationToken cancellationToken)
    {
        InvitationRecipientContext context = Authorize(await store.AcquireAsync(invitationId, cancellationToken), proof);
        DateTimeOffset now = clock.GetUtcNow();
        EnsurePending(context.Invitation, now);
        EnsureVersion(context.Invitation, expectedVersion);
        context.Invitation.Close("declined", context.Recipient.Id, now);
        await audit.StageAsync(IdentityAuditEntry.ForInvitationClosed(
            context.Organization.Id,
            context.Invitation.Id,
            context.Invitation.Version,
            "pending",
            "declined",
            context.Recipient.Id,
            now,
            IdentityAuditActor.User(context.Recipient.Id),
            now,
            SafeCorrelation(correlationId)), cancellationToken);
        return new InvitationDecline("declined", context.Invitation.Status, context.Invitation.Version);
    }

    private InvitationRecipientContext Authorize(InvitationRecipientContext? context, string? proof)
    {
        if (context is null || context.Superseded || User.NormalizeEmail(context.Recipient.PrimaryEmail) != context.Invitation.RecipientEmail ||
            !proofs.Verify(context.Organization.Id, context.Invitation.Id, proof, context.Invitation.TokenVerifier))
        {
            throw Unavailable();
        }

        return context;
    }

    private static bool OfferChanged(InvitationRecipientContext context)
    {
        if (context.Sponsor is null || context.Offers.Count == 0)
        {
            return true;
        }

        List<OrganizationRole> roles = context.Roles.Where(role => context.Offers.Any(offer => offer.RoleId == role.Id)).ToList();
        if (roles.Count != context.Offers.Count)
        {
            return true;
        }

        try
        {
            string capability = context.Invitation.ReplacesInvitationId.HasValue
                ? OrganizationCapabilityCatalog.InvitationsReissue : OrganizationCapabilityCatalog.InvitationsIssue;
            context.Sponsor.EnsureCanOfferRoles(capability, roles, context.Permissions);
        }
        catch (Exception exception) when (exception is ForbiddenException or ConflictException)
        {
            return true;
        }

        return context.Offers.Any(offer =>
        {
            OrganizationRole role = roles.Single(role => role.Id == offer.RoleId);
            string[] current = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, context.Permissions).Order(StringComparer.Ordinal).ToArray();
            return role.PermissionSetVersion != offer.OfferedPermissionSetVersion ||
                !current.SequenceEqual(offer.OfferedPermissionCodes.EnumerateArray().Select(item => item.GetString()!), StringComparer.Ordinal);
        });
    }

    private static InvitationAcceptance Accepted(string outcome, OrganizationMembership membership, IEnumerable<OrganizationRole> roles) =>
        new(outcome, membership.Id, membership.OrganizationId, roles.OrderBy(role => role.Id).Select(role => new OrganizationRoleResponse(role.Id, role.Code, role.Name)).ToArray(), membership.Version);

    private static void EnsurePending(OrganizationInvitation invitation, DateTimeOffset now)
    {
        if (invitation.Status != "pending" || invitation.ExpiresAt <= now)
        {
            throw new ConflictException("Invitation.Closed", "The invitation is closed or expired.");
        }
    }

    private static void EnsureVersion(OrganizationInvitation invitation, long expectedVersion)
    {
        if (invitation.Version != expectedVersion)
        {
            throw new ConflictException("Invitation.OfferChanged", "The invitation has changed.");
        }
    }

    private static ForbiddenException Unavailable() => new("Invitation.Unavailable", "The invitation is unavailable.");
    private static string SafeCorrelation(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 100 ? Guid.NewGuid().ToString("D") : value.Trim();
}
