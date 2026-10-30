using DeliveryPilot.Identity.Application.Abstractions.Auditing;
using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.RetireOrganizationRole;

public sealed class RetireOrganizationRoleHandler(IOrganizationMutationGuard guard, IOrganizationRoleStore store, IIdentityAuditWriter audit, TimeProvider clock)
    : ICommandHandler<RetireOrganizationRoleCommand, CreateOrganizationRoleResponse>
{
    public async Task<Result<CreateOrganizationRoleResponse>> HandleAsync(RetireOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        OrganizationMutationScope scope = await guard.AcquireAsync(request.OrganizationId, cancellationToken: cancellationToken);
        scope.EnsureCanDefineCustomRole([]);
        OrganizationRoleRead selected = await store.FindForUpdateAsync(request.OrganizationId, request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Organization.RoleNotFound", "Role was not found.");
        OrganizationRole role = selected.Role;
        if (role.Origin != "custom" || role.RetiredAt is not null)
        {
            throw new ConflictException("Organization.RoleImmutable", "The role cannot be retired.");
        }

        if (role.Version != request.ExpectedVersion)
        {
            throw new ConflictException("Organization.RoleStale", "The role has changed; refresh before editing.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        if (await store.HasRetirementReferencesAsync(request.OrganizationId, role.Id, now, cancellationToken))
        {
            throw new ConflictException("Organization.RoleInUse", "The role cannot be retired while it is in use.");
        }

        role.Retire(now);
        string correlation = string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 100 ? Guid.NewGuid().ToString("D") : request.CorrelationId;
        await audit.StageAsync(IdentityAuditEntry.ForRoleRetired(role, IdentityAuditActor.User(scope.Actor.UserId), now, correlation, request.Reason!.Trim()), cancellationToken);
        string[] codes = selected.Permissions.Select(item => item.PermissionCode).Order(StringComparer.Ordinal).ToArray();
        return Result.Success(new CreateOrganizationRoleResponse(role.Id, role.Code, role.Name, role.Description, role.Origin, codes, role.Version, role.PermissionSetVersion, role.RetiredAt));
    }
}
