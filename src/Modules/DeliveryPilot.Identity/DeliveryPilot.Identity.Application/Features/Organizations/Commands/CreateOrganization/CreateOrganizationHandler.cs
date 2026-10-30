using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;

public sealed class CreateOrganizationHandler(
    IOrganizationCreationStore store,
    IOrganizationMutationGuard guard,
    IOrganizationAuthorization authorization,
    IIdentityAuditWriter audit,
    TimeProvider timeProvider,
    ICurrentUserProvider? currentUserProvider = null) : ICommandHandler<CreateOrganizationCommand, CreateOrganizationResult>
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CreateOrganizationResult>> HandleAsync(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        CurrentUser? principal = currentUserProvider?.GetCurrentUser();
        if (!Guid.TryParse(principal?.Id, out Guid userId) || userId == Guid.Empty
            || !Guid.TryParse(principal.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        string name = request.Name!.Trim();
        byte[] fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        await store.LockAttemptAsync(userId, request.AttemptId, cancellationToken);
        OrganizationCreationAttempt? receipt = await store.FindAttemptAsync(userId, request.AttemptId, cancellationToken);
        if (receipt is not null)
        {
            OrganizationMutationScope scope = await guard.AcquireAsync(receipt.OrganizationId, cancellationToken: cancellationToken);
            await store.LockSessionAndCheckEligibilityAsync(userId, sessionId, cancellationToken);
            OrganizationMemberSecurityContext current = await authorization.AuthorizeCapabilityAsync(
                receipt.OrganizationId, OrganizationCapabilityCatalog.AuditRead, cancellationToken);
            receipt.ValidateSnapshot();
            OrganizationCreationSnapshot snapshot = JsonSerializer.Deserialize<OrganizationCreationSnapshot>(receipt.ResultSnapshot, SnapshotJsonOptions)
                ?? throw new InvalidOperationException("The creation receipt is invalid.");
            if (scope.ActorMembership.Id != snapshot.MembershipId || current.MembershipId != snapshot.MembershipId || current.UserId != userId)
            {
                throw OrganizationMutationScope.CreateAccessDenied();
            }

            if (timeProvider.GetUtcNow() >= receipt.ReplayUntil)
            {
                throw new ConflictException("Organization.CreationAttemptExpired", "This creation attempt has expired. Use a new key for a new intent.");
            }

            if (!CryptographicOperations.FixedTimeEquals(fingerprint, receipt.RequestFingerprint))
            {
                throw new ConflictException("Organization.CreationAttemptConflict", "This creation attempt was used for different content.");
            }

            return Result.Success(new CreateOrganizationResult(snapshot, true));
        }

        await store.LockUserAsync(userId, cancellationToken);
        await store.LockSessionAndCheckEligibilityAsync(userId, sessionId, cancellationToken);
        DateTimeOffset clock = timeProvider.GetUtcNow().ToUniversalTime();
        var now = new DateTimeOffset(clock.Ticks - (clock.Ticks % 10), TimeSpan.Zero);
        Organization organization = Organization.Create(name, userId, now);
        OrganizationBuiltInRoleSeed builtIns = OrganizationCapabilityCatalog.CreateBuiltInRoles(organization.Id, now);
        OrganizationMembership membership = OrganizationMembership.Create(organization.Id, userId, userId, now);
        OrganizationRoleAssignment assignment = OrganizationRoleAssignment.Create(organization.Id, membership.Id, builtIns.OwnerRole.Id, userId, now);
        var createdSnapshot = new OrganizationCreationSnapshot(
            organization.Id,
            organization.Name,
            organization.Status,
            membership.Id,
            [OrganizationCapabilityCatalog.OwnerRoleName],
            organization.Version,
            membership.Version,
            "creationSnapshot",
            now);
        string snapshotJson = JsonSerializer.Serialize(createdSnapshot, SnapshotJsonOptions);
        var attempt = OrganizationCreationAttempt.Create(userId, request.AttemptId, organization.Id, fingerprint, now, snapshotJson);
        store.Stage(organization, builtIns.Roles, builtIns.Permissions, membership, assignment, attempt);

        var actor = IdentityAuditActor.User(userId);
        string correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 100
            ? Guid.NewGuid().ToString("D")
            : request.CorrelationId;
        await audit.StageAsync(IdentityAuditEntry.ForOrganizationCreated(organization, actor, now, correlationId), cancellationToken);
        foreach (OrganizationRole role in builtIns.Roles)
        {
            IEnumerable<string> rolePermissions = builtIns.Permissions.Where(permission => permission.RoleId == role.Id).Select(permission => permission.PermissionCode);
            await audit.StageAsync(IdentityAuditEntry.ForRoleCreated(role, rolePermissions, actor, now, correlationId), cancellationToken);
        }

        await audit.StageAsync(IdentityAuditEntry.ForMembershipCreated(membership, actor, now, correlationId), cancellationToken);
        await audit.StageAsync(
            IdentityAuditEntry.ForRoleAssignmentGranted(assignment, membership.Version, builtIns.OwnerRole.PermissionSetVersion, actor, now, correlationId),
            cancellationToken);
        return Result.Success(new CreateOrganizationResult(createdSnapshot, false));
    }
}
