using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.ChangeRoleAssignment;

public sealed class ChangeRoleAssignmentHandler(IOrganizationMutationGuard guard, IOrganizationRoleStore store, IIdentityAuditWriter audit, TimeProvider clock) : ICommandHandler<ChangeRoleAssignmentCommand, ChangeRoleAssignmentResponse>
{
    public async Task<Result<ChangeRoleAssignmentResponse>> HandleAsync(ChangeRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(request.OrganizationId, request.MembershipId, cancellationToken);
        scope.EnsureCapability(OrganizationCapabilityCatalog.RolesAssign);
        OrganizationMembership membership = scope.TargetMembership ?? throw OrganizationMutationScope.CreateAccessDenied();
        OrganizationMemberSecurityContext target = scope.Target ?? throw OrganizationMutationScope.CreateAccessDenied();
        if (!target.IsEligibleActiveMember)
        {
            throw OrganizationMutationScope.CreateAccessDenied();
        }

        OrganizationRoleRead selected = await store.FindForUpdateAsync(request.OrganizationId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Organization.RoleNotFound", "Role was not found.");
        OrganizationRole role = selected.Role;
        if (request.Grant)
        {
            scope.EnsureCanGrantOrdinaryRole(role, selected.Permissions, target);
        }
        else
        {
            scope.EnsureCanRevokeOrdinaryRole(role, selected.Permissions, target);
            if (OrganizationCapabilityCatalog.IsSystemAdminRole(role) && string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new CqrsValidationException(["An Admin role revocation requires a reason."]);
            }
        }

        DateTimeOffset now = clock.GetUtcNow();
        OrganizationRoleAssignment? assignment = await store.FindAssignmentAsync(request.OrganizationId, membership.Id, role.Id, cancellationToken);
        bool active = assignment is not null && assignment.RevokedAt is null && (assignment.ExpiresAt is null || assignment.ExpiresAt > now);
        bool changed = request.Grant != active;
        if (changed && membership.Version != request.ExpectedMembershipVersion)
        {
            throw new ConflictException("Organization.MembershipStale", "The membership has changed; refresh before editing.");
        }

        if (changed)
        {
            string correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 100 ? Guid.NewGuid().ToString("D") : request.CorrelationId;
            var actor = IdentityAuditActor.User(scope.Actor.UserId);
            string? reason = request.Reason?.Trim();
            OrganizationRoleAssignmentAuditSnapshot? before = assignment is null ? null : OrganizationRoleAssignmentAuditSnapshot.Capture(assignment);
            if (request.Grant)
            {
                if (assignment is null)
                {
                    assignment = OrganizationRoleAssignment.Create(request.OrganizationId, membership.Id, role.Id, scope.Actor.UserId, now, grantReason: reason);
                    store.AddAssignment(assignment);
                }
                else
                {
                    assignment.Regrant(scope.Actor.UserId, now, reason: reason);
                }
            }
            else
            {
                assignment!.Revoke(scope.Actor.UserId, now, reason);
            }

            membership.RecordAssignmentChange(now);
            IdentityAuditEntry entry = request.Grant
                ? before is null
                    ? IdentityAuditEntry.ForRoleAssignmentGranted(assignment!, membership.Version, role.PermissionSetVersion, actor, now, correlationId, reason)
                    : IdentityAuditEntry.ForRoleAssignmentRegranted(before, assignment!, membership.Version, role.PermissionSetVersion, actor, now, correlationId, reason)
                : IdentityAuditEntry.ForRoleAssignmentRevoked(before!, assignment!, membership.Version, actor, now, correlationId, reason, OrganizationCapabilityCatalog.IsSystemAdminRole(role));
            await audit.StageAsync(entry, cancellationToken);
        }

        var roles = target.EffectiveRoles.Where(item => !changed || item.Id != role.Id).Select(item => new OrganizationRoleResponse(item.Id, item.Code, item.Name)).ToList();
        if (changed && request.Grant)
        {
            roles.Add(new OrganizationRoleResponse(role.Id, role.Code, role.Name));
        }

        return Result.Success(new ChangeRoleAssignmentResponse(membership.Id, membership.Version, roles, changed));
    }
}
