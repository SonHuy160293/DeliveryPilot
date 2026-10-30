using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Authorization;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;

public sealed class CreateOrganizationRoleHandler(IOrganizationMutationGuard guard, IOrganizationRoleStore store, IIdentityAuditWriter audit, TimeProvider clock) : ICommandHandler<CreateOrganizationRoleCommand, CreateOrganizationRoleResponse>
{
    public async Task<Result<CreateOrganizationRoleResponse>> HandleAsync(CreateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(request.OrganizationId, cancellationToken: cancellationToken);
        string[] codes = request.PermissionCodes!.Order(StringComparer.Ordinal).ToArray();
        scope.EnsureCanDefineCustomRole(codes);
        if (OrganizationCapabilityCatalog.IsReservedBuiltInRoleCodeOrName(request.Name))
        {
            throw new ConflictException("Organization.RoleNameReserved", "Role name is reserved.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        var role = OrganizationRole.CreateCustom(request.OrganizationId, request.Name!, scope.Actor.UserId, now, request.Description);
        OrganizationRolePermission[] permissions = codes.Select(code => OrganizationRolePermission.Create(role.OrganizationId, role.Id, code)).ToArray();
        store.Add(role, permissions);
        string correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 100 ? Guid.NewGuid().ToString("D") : request.CorrelationId;
        await audit.StageAsync(IdentityAuditEntry.ForRoleCreated(role, codes, IdentityAuditActor.User(scope.Actor.UserId), now, correlationId), cancellationToken);
        return Result.Success(new CreateOrganizationRoleResponse(role.Id, role.Code, role.Name, role.Description, role.Origin, codes, role.Version, role.PermissionSetVersion, role.RetiredAt));
    }
}
