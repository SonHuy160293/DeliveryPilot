using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.UpdateOrganizationRole;

public sealed class UpdateOrganizationRoleHandler(IOrganizationMutationGuard guard, IOrganizationRoleStore store, IIdentityAuditWriter audit, TimeProvider clock) : ICommandHandler<UpdateOrganizationRoleCommand, CreateOrganizationRoleResponse>
{
    public async Task<Result<CreateOrganizationRoleResponse>> HandleAsync(UpdateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(request.OrganizationId, cancellationToken: cancellationToken);
        scope.EnsureCanDefineCustomRole([]);
        OrganizationRoleRead existing = await store.FindForUpdateAsync(request.OrganizationId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Organization.RoleNotFound", "Role was not found.");
        OrganizationRole role = existing.Role;
        if (role.Origin != "custom" || role.RetiredAt is not null)
        {
            throw new ConflictException("Organization.RoleImmutable", "The role cannot be edited.");
        }

        if (OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName(request.Name))
        {
            throw new ConflictException("Organization.RoleNameReserved", "Role name is reserved.");
        }

        string[] beforeCodes = existing.Permissions.Select(permission => permission.PermissionCode).Order(StringComparer.Ordinal).ToArray();
        string[] codes = request.PermissionCodes!.Order(StringComparer.Ordinal).ToArray();
        string[] added = codes.Except(beforeCodes, StringComparer.Ordinal).ToArray();
        scope.EnsureCanDefineCustomRole(added);
        bool metadataChanged = role.Name != request.Name!.Trim() || role.Description != request.Description;
        bool permissionsChanged = !beforeCodes.SequenceEqual(codes, StringComparer.Ordinal);
        if ((metadataChanged || permissionsChanged) && role.Version != request.ExpectedVersion)
        {
            throw new ConflictException("Organization.RoleStale", "The role has changed; refresh before editing.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        string correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 100 ? Guid.NewGuid().ToString("D") : request.CorrelationId;
        var actor = IdentityAuditActor.User(scope.Actor.UserId);
        if (metadataChanged)
        {
            var before = OrganizationRoleAuditSnapshot.Capture(role, beforeCodes);
            role.UpdateDetails(request.Name!, request.Description, now);
            await audit.StageAsync(IdentityAuditEntry.ForRoleUpdated(before, role, actor, now, correlationId), cancellationToken);
        }

        if (permissionsChanged)
        {
            var before = OrganizationRoleAuditSnapshot.Capture(role, beforeCodes);
            role.AdvancePermissionSetVersion(now);
            OrganizationRolePermission[] removed = existing.Permissions.Where(permission => !codes.Contains(permission.PermissionCode, StringComparer.Ordinal)).ToArray();
            OrganizationRolePermission[] newPermissions = added.Select(code => OrganizationRolePermission.Create(role.OrganizationId, role.Id, code)).ToArray();
            store.UpdatePermissions(removed, newPermissions);
            await audit.StageAsync(IdentityAuditEntry.ForRolePermissionsChanged(before, role, codes, actor, now, correlationId), cancellationToken);
        }

        return Result.Success(new CreateOrganizationRoleResponse(role.Id, role.Code, role.Name, role.Description, role.Origin, codes, role.Version, role.PermissionSetVersion, role.RetiredAt));
    }
}
