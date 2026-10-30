using System.Text.Json;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations;

public sealed class InvitationIssuanceService(
    IOrganizationMutationGuard guard,
    IInvitationIssuanceGovernor governor,
    IInvitationStore store,
    IInvitationProofService proofs,
    IIdentityAuditWriter audit,
    TimeProvider clock,
    IInvitationDeliveryStager? stager = null)
{
    public async Task<OrganizationInvitation> IssueAsync(Guid organizationId, string email, IReadOnlyCollection<Guid> roleIds, string correlationId, CancellationToken cancellationToken = default)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, cancellationToken: cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.InvitationsIssue);
        string normalizedEmail = User.NormalizeEmail(email);
        DateTimeOffset now = clock.GetUtcNow();
        (IReadOnlyList<OrganizationRole> roles, IReadOnlyList<OrganizationRolePermission> permissions) = await LoadRolesAsync(scope, roleIds, OrganizationCapabilityCatalog.InvitationsIssue, cancellationToken);
        await EnsureEligibleAsync(scope, normalizedEmail, cancellationToken);
        OrganizationInvitation? existing = await store.FindPendingAsync(organizationId, normalizedEmail, cancellationToken);
        if (existing is not null && existing.ExpiresAt > now)
        {
            throw scope.Actor.HasPermission(OrganizationCapabilityCatalog.InvitationsRead)
                ? new ConflictException("Invitation.PendingOfferExists", "A pending invitation already exists.")
                : RecipientUnavailable();
        }

        await governor.CheckAsync(scope, normalizedEmail, OrganizationCapabilityCatalog.InvitationsIssue, cancellationToken);
        if (existing is not null)
        {
            await CloseAsync(existing, "expired", null, now, null, correlationId, cancellationToken);
        }

        return await StageNewAsync(scope, normalizedEmail, roles, permissions, now, null, IdentityAuditSchemaCatalog.InvitationIssued, correlationId, cancellationToken);
    }

    public async Task<OrganizationInvitation> ResendAsync(Guid organizationId, Guid invitationId, long expectedVersion, string correlationId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ValidateReason(reason, false);
        OrganizationMutationScope scope = await guard.AcquireForInvitationAsync(organizationId, invitationId, cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.InvitationsResend);
        scope.EnsurePrivilegedChangeAllowed();
        OrganizationInvitation invitation = await FindTargetAsync(scope, invitationId, cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();
        if (invitation.Status != "pending" || invitation.ExpiresAt <= now)
        {
            throw new ConflictException("Invitation.Closed", "The invitation cannot be resent.");
        }

        EnsureExpectedVersion(invitation, expectedVersion);

        IReadOnlyList<OrganizationInvitationRoleOffer> offers = await store.GetOffersAsync(organizationId, invitation.Id, cancellationToken);
        (IReadOnlyList<OrganizationRole> roles, IReadOnlyList<OrganizationRolePermission> permissions) = await LoadRolesAsync(scope, offers.Select(x => x.RoleId).ToArray(), OrganizationCapabilityCatalog.InvitationsResend, cancellationToken);
        await EnsureEligibleAsync(scope, invitation.RecipientEmail, cancellationToken);
        try
        {
            if (scope.Sponsor is null)
            {
                throw OrganizationMutationScope.CreateAccessDenied();
            }

            scope.Sponsor.EnsureCanOfferRoles(
                invitation.ReplacesInvitationId.HasValue ? OrganizationCapabilityCatalog.InvitationsReissue : OrganizationCapabilityCatalog.InvitationsIssue,
                roles,
                permissions);
        }
        catch (ForbiddenException)
        {
            throw new ConflictException("Invitation.OfferChanged", "The invitation must be reissued.");
        }

        foreach (OrganizationInvitationRoleOffer offer in offers)
        {
            OrganizationRole role = roles.Single(x => x.Id == offer.RoleId);
            string[] current = OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, permissions).Order(StringComparer.Ordinal).ToArray();
            string[] saved = offer.OfferedPermissionCodes.EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (role.PermissionSetVersion != offer.OfferedPermissionSetVersion || !current.SequenceEqual(saved, StringComparer.Ordinal))
            {
                throw new ConflictException("Invitation.OfferChanged", "The invitation must be reissued.");
            }
        }

        await governor.CheckAsync(scope, invitation.RecipientEmail, OrganizationCapabilityCatalog.InvitationsResend, cancellationToken);
        IInvitationDeliveryStager deliveryStager = RequireStager();
        Guid messageId = Guid.CreateVersion7();
        InvitationProof proof = proofs.Create(organizationId, invitation.Id);
        invitation.RotateProof(messageId, proof.Verifier, now, now.AddDays(7));
        var delivery = OrganizationInvitationDelivery.Create(organizationId, invitation.Id, messageId, now, invitation.ExpiresAt);
        store.AddDelivery(delivery);
        await StageAuditAsync(invitation, roles.Select(x => x.Id).ToArray(), scope.Actor.UserId, IdentityAuditSchemaCatalog.InvitationResent, now, correlationId, cancellationToken, reason);
        await deliveryStager.StageAsync(invitation, offers, delivery, proof, SafeCorrelation(correlationId), cancellationToken);
        return invitation;
    }

    public async Task<OrganizationInvitation> ReissueAsync(Guid organizationId, Guid invitationId, long expectedVersion, IReadOnlyCollection<Guid> roleIds, string reason, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidateReason(reason, true);

        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, cancellationToken: cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.InvitationsReissue);
        scope.EnsurePrivilegedChangeAllowed();
        OrganizationInvitation previous = await FindTargetAsync(scope, invitationId, cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();
        if (previous.Status != "pending" || previous.ExpiresAt <= now)
        {
            throw new ConflictException("Invitation.Closed", "The invitation cannot be reissued.");
        }

        EnsureExpectedVersion(previous, expectedVersion);

        (IReadOnlyList<OrganizationRole> roles, IReadOnlyList<OrganizationRolePermission> permissions) = await LoadRolesAsync(scope, roleIds, OrganizationCapabilityCatalog.InvitationsReissue, cancellationToken);
        await EnsureEligibleAsync(scope, previous.RecipientEmail, cancellationToken);
        await governor.CheckAsync(scope, previous.RecipientEmail, OrganizationCapabilityCatalog.InvitationsReissue, cancellationToken);
        RequireStager();
        await CloseAsync(previous, "cancelled", scope.Actor.UserId, now, reason, correlationId, cancellationToken);
        return await StageNewAsync(scope, previous.RecipientEmail, roles, permissions, now, previous.Id, IdentityAuditSchemaCatalog.InvitationReissued, correlationId, cancellationToken);
    }

    public async Task<OrganizationInvitation> CancelAsync(Guid organizationId, Guid invitationId, long expectedVersion, string reason, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidateReason(reason, true);
        if (expectedVersion <= 0)
        {
            throw new BadRequestException("Invitation.InvalidRequest", "A valid version and reason are required.");
        }

        OrganizationMutationScope scope = await guard.AcquireAsync(organizationId, cancellationToken: cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.InvitationsCancel);
        scope.EnsurePrivilegedChangeAllowed();
        OrganizationInvitation invitation = await FindTargetAsync(scope, invitationId, cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();
        if (invitation.Status != "pending" || invitation.ExpiresAt <= now)
        {
            throw new ConflictException("Invitation.Closed", "The invitation cannot be cancelled.");
        }

        if (invitation.Version != expectedVersion)
        {
            throw new ConflictException("Invitation.OfferChanged", "The invitation has changed.");
        }

        await CloseAsync(invitation, "cancelled", scope.Actor.UserId, now, reason, correlationId, cancellationToken);
        return invitation;
    }

    private async Task<OrganizationInvitation> StageNewAsync(OrganizationMutationScope scope, string email, IReadOnlyList<OrganizationRole> roles, IReadOnlyList<OrganizationRolePermission> permissions, DateTimeOffset now, Guid? previousId, string eventType, string correlationId, CancellationToken cancellationToken)
    {
        IInvitationDeliveryStager deliveryStager = RequireStager();
        Guid invitationId = Guid.CreateVersion7();
        Guid messageId = Guid.CreateVersion7();
        InvitationProof proof = proofs.Create(scope.Organization.Id, invitationId);
        var invitation = OrganizationInvitation.CreateWithId(invitationId, scope.Organization.Id, email, scope.Actor.UserId, now, now.AddDays(7), proof.Verifier, messageId, previousId);
        var offers = roles.OrderBy(x => x.Id).Select(role => OrganizationInvitationRoleOffer.Create(scope.Organization.Id, invitationId, role.Id, role.Name, role.PermissionSetVersion, JsonSerializer.SerializeToElement(OrganizationCapabilityCatalog.GetEffectiveRolePermissions(role, permissions).Order(StringComparer.Ordinal).ToArray()))).ToArray();
        var delivery = OrganizationInvitationDelivery.Create(scope.Organization.Id, invitationId, messageId, now, invitation.ExpiresAt);
        store.Add(invitation, offers, delivery);
        await StageAuditAsync(invitation, roles.Select(x => x.Id).ToArray(), scope.Actor.UserId, eventType, now, correlationId, cancellationToken);
        await deliveryStager.StageAsync(invitation, offers, delivery, proof, SafeCorrelation(correlationId), cancellationToken);
        return invitation;
    }

    private static void EnsureExpectedVersion(OrganizationInvitation invitation, long expectedVersion)
    {
        if (expectedVersion <= 0)
        {
            throw new BadRequestException("Invitation.InvalidRequest", "A valid version is required.");
        }

        if (invitation.Version != expectedVersion)
        {
            throw new ConflictException("Invitation.OfferChanged", "The invitation has changed.");
        }
    }

    private async Task<(IReadOnlyList<OrganizationRole>, IReadOnlyList<OrganizationRolePermission>)> LoadRolesAsync(OrganizationMutationScope scope, IReadOnlyCollection<Guid> roleIds, string capability, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0 || roleIds.Any(x => x == Guid.Empty) || roleIds.Distinct().Count() != roleIds.Count)
        {
            throw new BadRequestException("Invitation.InvalidRoles", "A distinct set of roles is required.");
        }

        IReadOnlyList<OrganizationRole> roles = await store.GetRolesAsync(scope.Organization.Id, roleIds, cancellationToken);
        if (roles.Count != roleIds.Count)
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        IReadOnlyList<OrganizationRolePermission> permissions = await store.GetPermissionsAsync(scope.Organization.Id, roleIds, cancellationToken);
        scope.EnsureCanOfferRoles(capability, roles, permissions);
        return (roles, permissions);
    }

    private async Task EnsureEligibleAsync(OrganizationMutationScope scope, string email, CancellationToken cancellationToken)
    {
        string? status = await store.GetRecipientMembershipStatusAsync(scope.Organization.Id, email, cancellationToken);
        if (status is "active" or "suspended")
        {
            throw RecipientUnavailable();
        }
    }

    private async Task CloseAsync(OrganizationInvitation invitation, string status, Guid? actorId, DateTimeOffset now, string? reason, string correlationId, CancellationToken cancellationToken)
    {
        await store.CloseForReplacementAsync(invitation, status, actorId, now, reason, cancellationToken);
        await audit.StageAsync(IdentityAuditEntry.ForInvitationClosed(invitation.OrganizationId, invitation.Id, invitation.Version, "pending", status, actorId, now, actorId.HasValue ? IdentityAuditActor.User(actorId.Value) : IdentityAuditActor.System("invitation-expiry"), now, SafeCorrelation(correlationId), reason), cancellationToken);
    }

    private Task StageAuditAsync(OrganizationInvitation invitation, Guid[] roleIds, Guid actorId, string eventType, DateTimeOffset now, string correlationId, CancellationToken cancellationToken, string? reason = null)
    {
        string details = JsonSerializer.Serialize(new { issuedByUserId = actorId, offeredRoleIds = roleIds, expiresAt = invitation.ExpiresAt, replacesInvitationId = invitation.ReplacesInvitationId });
        return audit.StageAsync(new IdentityAuditEntry(invitation.OrganizationId, null, null, IdentityAuditSchemaCatalog.OrganizationInvitationAggregate, invitation.Id, invitation.Version, eventType, IdentityAuditActor.User(actorId), now, SafeCorrelation(correlationId), details, reason), cancellationToken);
    }

    private async Task<OrganizationInvitation> FindTargetAsync(OrganizationMutationScope scope, Guid invitationId, CancellationToken cancellationToken)
    {
        OrganizationInvitation? invitation = await store.FindAsync(scope.Organization.Id, invitationId, cancellationToken);
        return invitation ?? throw (scope.Actor.HasPermission(OrganizationCapabilityCatalog.InvitationsRead)
            ? new NotFoundException("Invitation.NotFound", "Invitation was not found.")
            : OrganizationMutationScope.CreateAccessDenied());
    }

    private static ConflictException RecipientUnavailable() => new("Invitation.RecipientUnavailable", "An invitation cannot be issued for this recipient.");

    private static void ValidateReason(string? reason, bool required)
    {
        try
        {
            IdentityAuditSchemaCatalog.ValidateAndNormalizeReason(reason, required);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException("Invitation.InvalidRequest", "A valid reason is required.");
        }
    }

    private IInvitationDeliveryStager RequireStager() => stager ?? throw new InvalidOperationException("Invitation delivery staging is not configured.");
    private static string SafeCorrelation(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 100 ? Guid.NewGuid().ToString("D") : value.Trim();
}
